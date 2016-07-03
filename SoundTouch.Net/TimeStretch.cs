/*******************************************************************************
 *
 * License :
 *
 *  SoundTouch audio processing library
 *  Copyright (c) Olli Parviainen
 *  C# port Copyright (c) Olaf Woudenberg
 *
 *  This library is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 2.1 of the License, or (at your option) any later version.
 *
 *  This library is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 *  Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public
 *  License along with this library; if not, write to the Free Software
 *  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 ******************************************************************************/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SoundTouch.Utility;

namespace SoundTouch
{

    /// <summary>
    /// Sampled sound tempo changer/time stretch algorithm. Changes the sound
    /// tempo  while maintaining the original pitch by using a time domain
    /// WSOLA-like method  with several performance-increasing tweaks.
    /// </summary>
    internal abstract class TimeStretch<TSampleType, TLongSampleType> : FifoProcessor<TSampleType> 
        where TSampleType : struct
        where TLongSampleType : struct
    {
        private const float FLT_MIN = 1.175494351e-38F;
        private static readonly int SIZEOF_SAMPLETYPE = Marshal.SizeOf(typeof(TSampleType));
        private static readonly short[,] _scanOffsets = new short[5,24]
                                                            {
                                                                {
                                                                    124, 186, 248, 310, 372, 434, 496, 558, 620, 682, 744, 806,
                                                                    868, 930, 992, 1054, 1116, 1178, 1240, 1302, 1364, 1426, 1488, 0
                                                                },
                                                                {
                                                                    -100, -75, -50, -25, 25, 50, 75, 100, 0, 0, 0, 0,
                                                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                                                                },
                                                                {
                                                                    -20, -15, -10, -5, 5, 10, 15, 20, 0, 0, 0, 0,
                                                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                                                                },
                                                                {
                                                                    -4, -3, -2, -1, 1, 2, 3, 4, 0, 0, 0, 0,
                                                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                                                                },
                                                                {
                                                                    121, 114, 97, 114, 98, 105, 108, 32, 104, 99, 117, 111,
                                                                    116, 100, 110, 117, 111, 115, 0, 0, 0, 0, 0, 0
                                                                }
                                                            };
        
        private bool _autoSeekSetting;
        private bool _autoSeqSetting;
        private bool _quickSeek;

        protected int _channels;
        private readonly FifoSampleBuffer<TSampleType> _inputBuffer;
        private readonly FifoSampleBuffer<TSampleType> _outputBuffer;
        protected int _overlapDividerBitsNorm;
        protected int _overlapDividerBitsPure;
        protected int _overlapLength;
        private int _overlapMs;

        protected long _maxNorm;
        protected float _maxNormFloat;

        private double _tempo;
        private double _nominalSkip;
        private double _skipFract;

        protected TSampleType[] _midBuffer;
        protected int _sampleRate;
        private int _sampleReq;
        private int _seekLength;
        private int _seekWindowLength;
        private int _seekWindowMs;
        private int _sequenceMs;
        protected int _slopingDivider;

        protected TimeStretch()
            : this(new FifoSampleBuffer<TSampleType>())
        {
        }

        private TimeStretch(FifoSampleBuffer<TSampleType> outputBuffer)  : base(outputBuffer)
        {
            _inputBuffer = new FifoSampleBuffer<TSampleType>();
            _outputBuffer = outputBuffer;

            _quickSeek = false;
            _channels = 2;

            _midBuffer = null;
            _overlapLength = 0;

            _autoSeqSetting = true;
            _autoSeekSetting = true;

            _maxNorm = 0;
            _maxNormFloat = 1e8f;

            _skipFract = 0;

            _tempo = 1.0f;
            SetParameters(44100, Defaults.SEQUENCE_MS, Defaults.SEEKWINDOW_MS, Defaults.OVERLAP_MS);
            SetTempo(1.0f);

            Clear();
        }

        protected abstract void CalculateOverlapLength(int overlapMs);

        protected abstract double CalculateCrossCorr(ArrayPtr<TSampleType> mixingPos, ArrayPtr<TSampleType> compare, out double norm);
        protected abstract double CalculateCrossCorrAccumulate(ArrayPtr<TSampleType> mixingPos, ArrayPtr<TSampleType> compare, ref double norm);
        
        protected abstract void OverlapMono(ArrayPtr<TSampleType> pOutput, ArrayPtr<TSampleType> pInput);
        protected abstract void OverlapStereo(ArrayPtr<TSampleType> output, ArrayPtr<TSampleType> input);
        protected abstract void OverlapMulti(ArrayPtr<TSampleType> output, ArrayPtr<TSampleType> input);

        /// <exception cref="InvalidOperationException">Can't create a TimeStretch instance for type {0}. Only <c>short</c> and <c>float</c> are supported.</exception>
        public static TimeStretch<TSampleType, TLongSampleType> NewInstance()
        {
            if (typeof(TSampleType) == typeof(short))
                return (TimeStretch<TSampleType, TLongSampleType>) ((object)new TimeStretchInteger());
            if (typeof(TSampleType) == typeof(float))
                return (TimeStretch<TSampleType, TLongSampleType>) ((object)new TimeStretchFloat());

            throw new InvalidOperationException(string.Format("Can't create a TimeStretch instance for type {0}. Only <short> and <float> are supported.", typeof (TSampleType)));
        }

        /// <summary>
        /// Sets routine control parameters. These control are certain time
        /// constants defining how the sound is stretched to the desired
        /// duration.
        /// </summary>
        /// <param name="sampleRate">Sample rate of sound being processed (Hz)
        /// </param>
        /// <param name="sequenceDuration">one processing sequence length in
        /// milliseconds</param>
        /// <param name="seekWindowDuration">seeking window length for scanning
        /// the best overlapping position</param>
        /// <param name="overlapDuration">overlapping length</param>
        public void SetParameters(int sampleRate, int sequenceDuration = -1, int seekWindowDuration = -1, int overlapDuration = -1)
        {
            // accept only positive parameter values - if zero or negative, use old values instead
            if (sampleRate > 0) _sampleRate = sampleRate;
            if (overlapDuration > 0) _overlapMs = overlapDuration;

            if (sequenceDuration > 0)
            {
                _sequenceMs = sequenceDuration;
                _autoSeqSetting = false;
            }
            else if (sequenceDuration == 0)
            {
                // if zero, use automatic setting
                _autoSeqSetting = true;
            }

            if (seekWindowDuration > 0)
            {
                _seekWindowMs = seekWindowDuration;
                _autoSeekSetting = false;
            }
            else if (seekWindowDuration == 0)
            {
                // if zero, use automatic setting
                _autoSeekSetting = true;
            }

            CalcSeqParameters();

            CalculateOverlapLength(_overlapMs);

            // set tempo to recalculate '_sampleReq'
            SetTempo(_tempo);
        }

        /// <summary>Get routine control parameters, see setParameters() function.</summary>
        public void GetParameters(out int pSampleRate, out int pSequenceMs, out int pSeekWindowMs, out int pOverlapMs)
        {
            pSampleRate = _sampleRate;
            
            pSequenceMs = (_autoSeqSetting) ? (Defaults.USE_AUTO_SEQUENCE_LEN) : _sequenceMs;
            
            pSeekWindowMs = (_autoSeekSetting) ? (Defaults.USE_AUTO_SEEKWINDOW_LEN) : _seekWindowMs;
            
            pOverlapMs = _overlapMs;
        }
        
        private void ClearMidBuffer()
        {
            ArrayPtr<TSampleType>.Fill(_midBuffer, default(TSampleType), _channels*_overlapLength);
        }

        /// <summary>Clears the input buffer</summary>
        public void ClearInput()
        {
            _inputBuffer.Clear();
            ClearMidBuffer();
        }

        /// <summary>Clears the sample buffers</summary>
        public override void Clear()
        {
            _outputBuffer.Clear();
            ClearInput();
        }

        /// <summary>
        /// Enables/disables the quick position seeking algorithm. <c>false</c>
        /// to disable, <c>true</c> to enable
        /// </summary>
        public void EnableQuickSeek(bool enable)
        {
            _quickSeek = enable;
        }

        /// <summary>Returns <c>true</c> if the quick seeking algorithm is
        /// enabled.</summary>
        public bool IsQuickSeekEnabled
        {
            get { return _quickSeek; }
        }

        protected virtual int SeekBestOverlapPosition(ArrayPtr<TSampleType> refPos)
        {
            if (_quickSeek)
                return SeekBestOverlapPositionQuick(refPos);
            return SeekBestOverlapPositionFull(refPos);
        }

        /// <summary>
        /// Overlaps samples in <see cref="_midBuffer"/> with the samples in 
        /// <paramref name="pInput"/> at position of <paramref name="ovlPos"/>.
        /// </summary>
        private void Overlap(ArrayPtr<TSampleType> pOutput, ArrayPtr<TSampleType> pInput, int ovlPos)
        {
#if !USE_MULTICH_ALWAYS
            if (_channels == 1)
            {
                // mono sound.
                OverlapMono(pOutput, pInput + ovlPos);
            }
            else  if (_channels == 2)
            {
                // stereo sound
                OverlapStereo(pOutput, pInput + 2*ovlPos);
            }
            else
#endif
            {
                Debug.Assert(_channels > 0);
                OverlapMulti(pOutput, pInput + _channels * ovlPos);
            }
        }

        /// <summary>Seeks for the optimal overlap-mixing position. The 'stereo'
        /// version of the routine
        ///
        /// The best position is determined as the position where the two
        /// overlapped sample sequences are 'most alike', in terms of the
        /// highest cross-correlation value over the overlapping period
        ///</summary>
        protected virtual int SeekBestOverlapPositionFull(ArrayPtr<TSampleType> refPos)
        {
            double bestCorr = FLT_MIN;
            int bestOffs = 0;
            double norm = 0;

            // Scans for the best correlation value by testing each possible position
            // over the permitted range.
            bestCorr = CalculateCrossCorr(refPos, _midBuffer, out norm);

            object parallelCritial = new object();

            Parallel.For(0, _seekLength, (int i) =>
            {
                // Calculates correlation value for the mixing position corresponding to 'i'
                var corr = CalculateCrossCorr(refPos + _channels*i, _midBuffer, out norm);

                // heuristic rule to slightly favour values close to mid of the range
                double tmp = (2*i - _seekLength)/(double) _seekLength;
                corr = ((corr + 0.1)*(1.0 - 0.25*tmp*tmp));

                // Checks for the highest correlation value
                lock (parallelCritial)
                    if (corr > bestCorr)
                    {
                        bestCorr = corr;
                        bestOffs = i;
                    }
            });

            // clear cross correlation routine state if necessary (is so e.g. in MMX routines).
            ClearCrossCorrState();

            return bestOffs;
        }

        /// <summary>
        /// Quick seek algorithm for improved runtime-performance: First roughly scans through the 
        /// correlation area, and then scan surroundings of two best preliminary correlation candidates
        /// with improved precision
        ///
        /// Based on testing:
        /// - This algorithm gives on average 99% as good match as the full algorith
        /// - this quick seek algorithm finds the best match on ~90% of cases
        /// - on those 10% of cases when this algorithm doesn't find best match, 
        ///   it still finds on average ~90% match vs. the best possible match
        /// </summary>
        protected virtual int SeekBestOverlapPositionQuick(ArrayPtr<TSampleType> refPos)
        {
            const int SCANSTEP = 16;
            const int SCANWIND = 8;

            int bestOffs;
            int i;
            int bestOffs2;
            float bestCorr, corr;
            float bestCorr2;
            double norm;

            // note: 'float' types used in this function in case that the platform would need to use software-fp

            bestCorr = FLT_MIN;
            bestOffs = SCANWIND;
            bestCorr2 = FLT_MIN;
            bestOffs2 = 0;

            int best = 0;

            // Scans for the best correlation value by testing each possible position
            // over the permitted range. Look for two best matches on the first pass to
            // increase possibility of ideal match.
            //
            // Begin from "SCANSTEP" instead of SCANWIND to make the calculation
            // catch the 'middlepoint' of seekLength vector as that's the a-priori 
            // expected best match position
            //
            // Roughly:
            // - 15% of cases find best result directly on the first round,
            // - 75% cases find better match on 2nd round around the best match from 1st round
            // - 10% cases find better match on 2nd round around the 2nd-best-match from 1st round
            for (i = SCANSTEP; i < _seekLength - SCANWIND - 1; i += SCANSTEP)
            {
                // Calculates correlation value for the mixing position corresponding
                // to 'i'
                corr = (float)CalculateCrossCorr(refPos + _channels * i, _midBuffer, out norm);
                // heuristic rule to slightly favour values close to mid of the seek range
                float tmp = (float)(2 * i - _seekLength - 1) / (float)_seekLength;
                corr = ((corr + 0.1f) * (1.0f - 0.25f * tmp * tmp));

                // Checks for the highest correlation value
                if (corr > bestCorr)
                {
                    // found new best match. keep the previous best as 2nd best match
                    bestCorr2 = bestCorr;
                    bestOffs2 = bestOffs;
                    bestCorr = corr;
                    bestOffs = i;
                }
                else if (corr > bestCorr2)
                {
                    // not new best, but still new 2nd best match
                    bestCorr2 = corr;
                    bestOffs2 = i;
                }
            }

            // Scans surroundings of the found best match with small stepping
            int end = Math.Min(bestOffs + SCANWIND + 1, _seekLength);
            for (i = bestOffs - SCANWIND; i < end; i++)
            {
                if (i == bestOffs) continue;    // this offset already calculated, thus skip

                // Calculates correlation value for the mixing position corresponding
                // to 'i'
                corr = (float)CalculateCrossCorr(refPos + _channels * i, _midBuffer, out norm);
                // heuristic rule to slightly favour values close to mid of the range
                float tmp = (float)(2 * i - _seekLength - 1) / (float)_seekLength;
                corr = ((corr + 0.1f) * (1.0f - 0.25f * tmp * tmp));

                // Checks for the highest correlation value
                if (corr > bestCorr)
                {
                    bestCorr = corr;
                    bestOffs = i;
                    best = 1;
                }
            }

            // Scans surroundings of the 2nd best match with small stepping
            end = Math.Min(bestOffs2 + SCANWIND + 1, _seekLength);
            for (i = bestOffs2 - SCANWIND; i < end; i++)
            {
                if (i == bestOffs2) continue;    // this offset already calculated, thus skip

                // Calculates correlation value for the mixing position corresponding
                // to 'i'
                corr = (float)CalculateCrossCorr(refPos + _channels * i, _midBuffer, out norm);
                // heuristic rule to slightly favour values close to mid of the range
                float tmp = (float)(2 * i - _seekLength - 1) / (float)_seekLength;
                corr = ((corr + 0.1f) * (1.0f - 0.25f * tmp * tmp));

                // Checks for the highest correlation value
                if (corr > bestCorr)
                {
                    bestCorr = corr;
                    bestOffs = i;
                    best = 2;
                }
            }

            // clear cross correlation routine state if necessary (is so e.g. in MMX routines).

            // clear cross correlation routine state if necessary (is so e.g. in MMX routines).
            ClearCrossCorrState();
            
            AdaptNormalizer();

            return bestOffs;
        }

        protected abstract void AdaptNormalizer();

        /// <summary>
        /// clear cross correlation routine state if necessary 
        /// </summary>
        protected virtual void ClearCrossCorrState()
        {
            // default implementation is empty.
        }

        private static double CheckLimits(double x, double mi, double ma)
        {
            return (((x) < (mi)) ? (mi) : (((x) > (ma)) ? (ma) : (x)));
        }

        /// <summary>
        /// Calculates processing sequence length according to tempo setting
        /// </summary>
        private void CalcSeqParameters()
        {
            // Adjust tempo param according to tempo, so that variating processing sequence length is used
            // at varius tempo settings, between the given low...top limits
            const float AUTOSEQ_TEMPO_LOW = 0.5f; // auto setting low tempo range (-50%)
            const float AUTOSEQ_TEMPO_TOP = 2.0f; // auto setting top tempo range (+100%)

            // sequence-ms setting values at above low & top tempo
            const float AUTOSEQ_AT_MIN = 125.0f;
            const float AUTOSEQ_AT_MAX = 50.0f;
            const float AUTOSEQ_K = ((AUTOSEQ_AT_MAX - AUTOSEQ_AT_MIN)/(AUTOSEQ_TEMPO_TOP - AUTOSEQ_TEMPO_LOW));
            const float AUTOSEQ_C = (AUTOSEQ_AT_MIN - (AUTOSEQ_K)*(AUTOSEQ_TEMPO_LOW));

            // seek-window-ms setting values at above low & top tempo
            const float AUTOSEEK_AT_MIN = 25.0f;
            const float AUTOSEEK_AT_MAX = 15.0f;
            const float AUTOSEEK_K = ((AUTOSEEK_AT_MAX - AUTOSEEK_AT_MIN)/(AUTOSEQ_TEMPO_TOP - AUTOSEQ_TEMPO_LOW));
            const float AUTOSEEK_C = (AUTOSEEK_AT_MIN - (AUTOSEEK_K)*(AUTOSEQ_TEMPO_LOW));


            if (_autoSeqSetting)
            {
                double seq = AUTOSEQ_C + AUTOSEQ_K*_tempo;
                seq = CheckLimits(seq, AUTOSEQ_AT_MAX, AUTOSEQ_AT_MIN);
                _sequenceMs = (int) (seq + 0.5);
            }

            if (_autoSeekSetting)
            {
                double seek = AUTOSEEK_C + AUTOSEEK_K*_tempo;
                seek = CheckLimits(seek, AUTOSEEK_AT_MAX, AUTOSEEK_AT_MIN);
                _seekWindowMs = (int) (seek + 0.5);
            }

            // Update seek window lengths
            _seekWindowLength = (_sampleRate*_sequenceMs)/1000;
            if (_seekWindowLength < 2*_overlapLength)
            {
                _seekWindowLength = 2*_overlapLength;
            }
            _seekLength = (_sampleRate*_seekWindowMs)/1000;
        }

        /// <summary>
        /// Sets new target tempo. Normal tempo = 'SCALE', smaller values
        /// represent slower  tempo, larger faster tempo.
        /// </summary>
        public void SetTempo(double newTempo)
        {
            int intskip;

            _tempo = newTempo;

            // Calculate new sequence duration
            CalcSeqParameters();

            // Calculate ideal skip length (according to tempo value) 
            _nominalSkip = _tempo*(_seekWindowLength - _overlapLength);
            intskip = (int) (_nominalSkip + 0.5);

            // Calculate how many samples are needed in the '_inputBuffer' to 
            // process another batch of samples
            //_sampleReq = max(intskip + _overlapLength, _seekWindowLength) + _seekLength / 2;
            _sampleReq = Math.Max(intskip + _overlapLength, _seekWindowLength) + _seekLength;
        }

        /// <summary>Sets the number of channels, 1 = mono, 2 = stereo</summary>
        public void SetChannels(int numChannels)
        {
            Debug.Assert(numChannels > 0);
            if (_channels == numChannels) return;
            // Debug.Assert(numChannels == 1 || numChannels == 2);

            _channels = numChannels;
            _inputBuffer.SetChannels(_channels);
            _outputBuffer.SetChannels(_channels);

            // re-init overlap/buffer
            _overlapLength = 0;
            SetParameters(_sampleRate);
        }

        /// <summary>
        /// Processes as many processing frames of the samples <see cref="_inputBuffer"/>, store
        /// the result into <see cref="_outputBuffer"/>
        /// </summary>
        private void ProcessSamples()
        {
            // Process samples as long as there are enough samples in '_inputBuffer'
            // to form a processing frame.
            while (_inputBuffer.AvailableSamples >= _sampleReq)
            {
                // If tempo differs from the normal ('SCALE'), scan for the best overlapping
                // position
                int offset = SeekBestOverlapPosition(_inputBuffer.PtrBegin());

                // Mix the samples in the '_inputBuffer' at position of 'offset' with the 
                // samples in 'midBuffer' using sliding overlapping
                // ... first partially overlap with the end of the previous sequence
                // (that's in 'midBuffer')
                Overlap(_outputBuffer.PtrEnd(_overlapLength), _inputBuffer.PtrBegin(), offset);
                _outputBuffer.PutSamples(_overlapLength);

                // ... then copy sequence samples from '_inputBuffer' to output:

                // length of sequence
                int temp = (_seekWindowLength - 2*_overlapLength);

                // crosscheck that we don't have buffer overflow...
                if (_inputBuffer.AvailableSamples < (offset + temp + _overlapLength*2))
                {
                    continue; // just in case, shouldn't really happen
                }

                _outputBuffer.PutSamples(_inputBuffer.PtrBegin() + _channels*(offset + _overlapLength), temp);

                // Copies the end of the current sequence from '_inputBuffer' to 
                // 'midBuffer' for being mixed with the beginning of the next 
                // processing sequence and so on
                Debug.Assert((offset + temp + _overlapLength*2) <= _inputBuffer.AvailableSamples);
                ArrayPtr<TSampleType>.CopyBytes(_midBuffer, _inputBuffer.PtrBegin() + _channels*(offset + temp + _overlapLength),
                                                _channels*SIZEOF_SAMPLETYPE*_overlapLength);

                // Remove the processed samples from the input buffer. Update
                // the difference between integer & nominal skip step to '_skipFract'
                // in order to prevent the error from accumulating over time.
                _skipFract += _nominalSkip; // real skip size
                var ovlSkip = (int) _skipFract;
                _skipFract -= ovlSkip; // maintain the fraction part, i.e. real vs. integer skip
                _inputBuffer.ReceiveSamples(ovlSkip);
            }
        }


        /// <summary>
        /// Adds <paramref name="nSamples"/> pcs of samples from the 
        /// <paramref name="samples"/> memory position into the input of the
        /// object.
        /// </summary>
        /// <param name="samples">Input sample data.</param>
        /// <param name="nSamples">Number of samples in 'samples' so that one
        /// sample  contains both channels if stereo.</param>
        public override void PutSamples(ArrayPtr<TSampleType> samples, int nSamples)
        {
            // Add the samples into the input buffer
            _inputBuffer.PutSamples(samples, nSamples);
            // Process the samples in input buffer
            ProcessSamples();
        }

        protected void AcceptNewOverlapLength(int newOverlapLength)
        {
            Debug.Assert(newOverlapLength >= 0);
            int prevOvl = _overlapLength;
            _overlapLength = newOverlapLength;

            if (_overlapLength > prevOvl)
            {
                _midBuffer = new TSampleType[_overlapLength * _channels];

                ClearMidBuffer();
            }
        }


        /// <summary>Returns the output buffer object</summary>
        public FifoSamplePipe<TSampleType> GetOutput()
        {
            return _outputBuffer;
        }

        /// <summary>Returns the input buffer object</summary>
        public FifoSamplePipe<TSampleType> GetInput()
        {
            return _inputBuffer;
        }

        /// <summary>return nominal input sample requirement for triggering a
        /// processing batch</summary>
        public int GetInputSampleReq()
        {
            return (int) (_nominalSkip + 0.5);
        }

        /// <summary>return nominal output sample amount when running a
        /// processing batch</summary>
        public int GetOutputBatchSize()
        {
            return _seekWindowLength - _overlapLength;
        }
    }
}