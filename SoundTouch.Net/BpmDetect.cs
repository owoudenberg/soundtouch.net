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
using System.IO;
using SoundTouch.Utility;

namespace SoundTouch
{
    /// <summary>
    /// Beats-per-minute (BPM) detection routine.
    ///
    /// The beat detection algorithm works as follows:
    /// - Use function 'inputSamples' to input a chunks of samples to the class for
    ///   analysis. It's a good idea to enter a large sound file or stream in smallish
    ///   chunks of around few kilo samples in order not to extinguish too much RAM memory.
    /// 
    /// - Input sound data is decimated to approx 500 Hz to reduce calculation burden,
    ///   which is basically ok as low (bass) frequencies mostly determine the beat rate.
    ///   Simple averaging is used for anti-alias filtering because the resulting signal
    ///   quality isn't of that high importance.
    /// 
    /// - Decimated sound data is enveloped, i.e. the amplitude shape is detected by
    ///   taking absolute value that's smoothed by sliding average. Signal levels that
    ///   are below a couple of times the general RMS amplitude level are cut away to
    ///   leave only notable peaks there.
    /// 
    /// - Repeating sound patterns (e.g. beats) are detected by calculating short-term 
    ///   autocorrelation function of the enveloped signal.
    /// 
    /// - After whole sound data file has been analyzed as above, the bpm level is 
    ///   detected by function <see cref="GetBpm"/> that finds the highest peak of the autocorrelation 
    ///   function, calculates it's precise location and converts this reading to bpm's.
    /// </summary>
    public abstract class BpmDetect<TSampleType, TLongSampleType>
        where TSampleType : struct
        where TLongSampleType : struct
    {
        /// Minimum allowed BPM rate. Used to restrict accepted result above a reasonable limit.
        private const int MIN_BPM = 29;

        /// Maximum allowed BPM rate. Used to restrict accepted result below a reasonable limit.
        private const int MAX_BPM = 230;

        private const int INPUT_BLOCK_SAMPLES = 2048;
        private const int DECIMATED_BLOCK_SAMPLES = 256;

        /// decay constant for calculating RMS volume sliding average approximation 
        /// (time constant is about 10 sec)
        private const float AVGDECAY = 0.99986f;

        /// Normalization coefficient for calculating RMS sliding average approximation.
        protected const float Avgnorm = (1 - AVGDECAY);

        /// FIFO-buffer for decimated processing samples.
        protected readonly FifoSampleBuffer<TSampleType> Buffer;

        /// Number of channels (1 = mono, 2 = stereo)
        protected readonly int Channels;

        /// Decimate sound by this coefficient to reach approx. 500 Hz.
        protected readonly int DecimateBy;

        /// Auto-correlation window length
        protected readonly int WindowLen;

        /// Beginning of auto-correlation window: Autocorrelation isn't being updated for
        /// the first these many correlation bins.
        protected readonly int WindowStart;

        /// Auto-correlation accumulator bins.
        protected readonly float[] Xcorr;

        /// sample rate
        private readonly int _sampleRate;

        /// Sample average counter.
        protected int DecimateCount;

        /// Sample average accumulator for FIFO-like decimation.
        protected TLongSampleType DecimateSum;

        /// RMS volume sliding average approximation level accumulator
        protected double RmsVolumeAccu;

        /// Accumulator for accounting what proportion of samples exceed cutCoeff level
        private double _aboveCutAccu;

        /// Level below which to cut off signals
        private double _cutCoeff;

        /// Amplitude envelope sliding average approximation level accumulator
        private double _envelopeAccu;

        /// Accumulator for total samples to calculate proportion of samples that exceed cutCoeff level
        private double _totalAccu;

        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="BpmDetect&lt;TSampleType, TLongSampleType&gt;"/> class.
        /// </summary>
        /// <param name="numChannels">Number of channels in sample data.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        protected BpmDetect(int numChannels, int sampleRate)
        {
            _sampleRate = sampleRate;
            Channels = numChannels;

            DecimateSum = default(TLongSampleType);
            DecimateCount = 0;

            _envelopeAccu = 0;

            _cutCoeff = 1.75;
            _aboveCutAccu = 0;
            _totalAccu = 0;

            // choose decimation factor so that result is approx. 500 Hz
            DecimateBy = sampleRate/500;
            Debug.Assert(DecimateBy > 0);
            Debug.Assert(INPUT_BLOCK_SAMPLES < DecimateBy*DECIMATED_BLOCK_SAMPLES);

            // Calculate window length & starting item according to desired min & max bpms
            WindowLen = (60*sampleRate)/(DecimateBy*MIN_BPM);
            WindowStart = (60*sampleRate)/(DecimateBy*MAX_BPM);

            Debug.Assert(WindowLen > WindowStart);

            // allocate new working objects
            Xcorr = new float[WindowLen];

            // allocate processing buffer
            Buffer = new FifoSampleBuffer<TSampleType>();
            // we do processing in mono mode
            Buffer.SetChannels(1);
            Buffer.Clear();
        }

        [Conditional("_CREATE_BPM_DEBUG_FILE")]
        private static void _SaveDebugData(float[] data, int minpos, int maxpos, double coeff)
        {
            const string DEBUGFILE_NAME = @"c:\temp\soundtouch-bpm-debug.txt";

            using (var fptr = new StreamWriter(DEBUGFILE_NAME, false))
            {
                int i;

                Console.Error.WriteLine("\n\nWriting BPM debug data into file " + DEBUGFILE_NAME + "\n\n");
                for (i = minpos; i < maxpos; i ++)
                {
                    fptr.WriteLine("{0}\t{1:.0}\t{2}", i, coeff/i, data[i]);
                }
            }
        }

        /// <summary>
        /// Updates auto-correlation function for given number of decimated
        /// samples that are read from the internal 'buffer' pipe (samples
        /// aren't removed from the pipe though).
        /// </summary>
        /// <param name="processSamples">How many samples are processed.</param>
        /// 
        protected abstract void UpdateXCorr(int processSamples);

        /// <summary>
        /// Decimates samples to approx. 500 Hz.
        /// </summary>
        /// <param name="dst">Destination buffer.</param>
        /// <param name="src">Source sample buffer.</param>
        /// <param name="numSamples">Number of source samples.</param>
        /// <returns>Number of output samples</returns>
        protected abstract int Decimate(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples);

        /// <summary>
        /// Returns the absolute value of a single-precision floating-point number. 
        /// </summary>
        protected abstract double Abs(TSampleType value);

        /// <summary>
        /// Calculates amplitude envelope for the buffer of samples.
        /// Result is output to <paramref cref="samples"/>.
        /// </summary>
        /// <param name="samples">Pointer to input/output data buffer.</param>
        /// <param name="numsamples">Number of samples in buffer.</param>
        private void CalcEnvelope(ArrayPtr<TSampleType> samples, int numsamples)
        {
            const double decay = 0.7f; // decay constant for smoothing the envelope
            const double norm = (1 - decay);

            for (int i = 0; i < numsamples; i ++)
            {
                // calc average RMS volume
                RmsVolumeAccu *= AVGDECAY;
                double val = Abs(samples[i]);
                RmsVolumeAccu += val*val;

                // cut amplitudes that are below cutoff ~2 times RMS volume
                // (we're interested in peak values, not the silent moments)
                val -= _cutCoeff*Math.Sqrt(RmsVolumeAccu*Avgnorm);
                if (val > 0)
                {
                    _aboveCutAccu += 1.0; // sample above threshold
                }
                else
                {
                    val = 0;
                }

                _totalAccu += 1.0;

                // maintain sliding statistic what proportion of 'val' samples is
                // above cutoff threshold
                _aboveCutAccu *= 0.99931; // 2 sec time constant
                _totalAccu *= 0.99931;

                if (_totalAccu > 500)
                {
                    // after initial settling, auto-adjust cutoff level so that ~8% of 
                    // values are above the threshold
                    double d = (_aboveCutAccu/_totalAccu) - 0.08;
                    _cutCoeff += 0.001*d;
                }

                // smooth amplitude envelope
                _envelopeAccu *= decay;
                _envelopeAccu += val;

                samples[i] = CutPeaks((_envelopeAccu*norm));
            }

            // check that cutoff doesn't get too small - it can be just silent sequence!
            if (_cutCoeff < 1.5)
            {
                _cutCoeff = 1.5;
            }
        }

        protected abstract TSampleType CutPeaks(double value);

        /// <summary>
        /// Inputs a block of samples for analyzing: Envelopes the samples and
        /// then updates the autocorrelation estimation. When whole song data
        /// has been input in smaller blocks using this function, read the
        /// resulting bpm with <see cref="GetBpm"/> function.
        /// </summary>
        /// <remarks>
        /// Notice that data in <paramref name="samples"/> array can be
        /// disrupted in processing.
        /// </remarks>
        /// <param name="samples">Pointer to input/working data buffer.</param>
        /// <param name="numSamples">Number of samples in buffer.</param>
        public void InputSamples(ArrayPtr<TSampleType> samples, int numSamples)
        {
            var decimated = new TSampleType[DECIMATED_BLOCK_SAMPLES];

            // iterate so that max INPUT_BLOCK_SAMPLES processed per iteration
            while (numSamples > 0)
            {
                int block = (numSamples > INPUT_BLOCK_SAMPLES) ? INPUT_BLOCK_SAMPLES : numSamples;

                // decimate. note that converts to mono at the same time
                int decSamples = Decimate(decimated, samples, block);
                samples += block*Channels;
                numSamples -= block;

                // envelope new samples and add them to buffer
                CalcEnvelope(decimated, decSamples);
                Buffer.PutSamples(decimated, decSamples);
            }

            // when the buffer has enought samples for processing...
            if (Buffer.AvailableSamples > WindowLen)
            {
                // how many samples are processed
                int processLength = Buffer.AvailableSamples - WindowLen;

                // ... calculate autocorrelations for oldest samples...
                UpdateXCorr(processLength);
                // ... and remove them from the buffer
                Buffer.ReceiveSamples(processLength);
            }
        }


        /// <summary>
        /// Analyzes the results and returns the BPM rate. Use this function to
        /// read result after whole song data has been input to the class by
        /// consecutive calls of 'inputSamples' function.
        /// </summary>
        /// <returns>Beats-per-minute rate, or zero if detection failed.
        /// </returns>
        public float GetBpm()
        {
            var peakFinder = new PeakFinder();

            double coeff = 60.0*(_sampleRate/(double) DecimateBy);

            // save bpm debug analysis data if debug data enabled
            _SaveDebugData(Xcorr, WindowStart, WindowLen, coeff);

            // find peak position
            double peakPos = peakFinder.DetectPeak(Xcorr, WindowStart, WindowLen);

            Debug.Assert(DecimateBy != 0);
            if (peakPos < 1e-9) return 0.0f; // detection failed.

            // calculate BPM
            return (float) (coeff/peakPos);
        }

        /// <exception cref="InvalidOperationException">Can't create a 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}"/> instance for
        /// specified type. Only <c>short</c> and <c>float</c> are supported
        /// </exception>
        public static BpmDetect<TSampleType, TLongSampleType> NewInstance(int getNumChannels, int getSampleRate)
        {
            if (typeof (TSampleType) == typeof (short))
                return (BpmDetect<TSampleType, TLongSampleType>) ((object) new BpmDetectInteger(getNumChannels, getSampleRate));
            if (typeof (TSampleType) == typeof (float))
                return (BpmDetect<TSampleType, TLongSampleType>) ((object) new BpmDetectFloat(getNumChannels, getSampleRate));

            throw new InvalidOperationException(string.Format("Can't create a TimeStretch instance for type {0}. Only <short> and <float> are supported.", typeof (TSampleType)));
        }
    }

    public sealed class BpmDetectInteger : BpmDetect<short, long>
    {
        public BpmDetectInteger(int numChannels, int sampleRate) : base(numChannels, sampleRate)
        {
            // Initialize RMS volume accumulator to RMS level of 3000 (out of 32768) that's
            // a typical RMS signal level value for song data. This value is then adapted
            // to the actual level during processing.

            // integer samples
            RmsVolumeAccu = (3000*3000)/Avgnorm;
        }

        protected override void UpdateXCorr(int processSamples)
        {
            int offs;

            Debug.Assert(Buffer.AvailableSamples >= (uint) (processSamples + WindowLen));

            ArrayPtr<short> pBuffer = Buffer.PtrBegin();
            for (offs = WindowStart; offs < WindowLen; offs ++)
            {
                long sum = 0;
                for (int i = 0; i < processSamples; i ++)
                {
                    sum += pBuffer[i]*pBuffer[i + offs]; // scaling the sub-result shouldn't be necessary
                }

                Xcorr[offs] += sum;
            }
        }

        protected override int Decimate(ArrayPtr<short> dst, ArrayPtr<short> src, int numSamples)
        {
            int count;

            Debug.Assert(Channels > 0);
            Debug.Assert(DecimateBy > 0);
            int outcount = 0;
            for (count = 0; count < numSamples; count ++)
            {
                int j;

                // convert to mono and accumulate
                for (j = 0; j < Channels; j ++)
                {
                    DecimateSum += src[j];
                }
                src += j;

                DecimateCount ++;
                if (DecimateCount >= DecimateBy)
                {
                    // Store every Nth sample only
                    long nthSample = (DecimateSum/(DecimateBy*Channels));
                    DecimateSum = 0;
                    DecimateCount = 0;
                    // check ranges for sure (shouldn't actually be necessary)
                    if (nthSample > 32767)
                    {
                        nthSample = 32767;
                    }
                    else if (nthSample < -32768)
                    {
                        nthSample = -32768;
                    }

                    dst[outcount] = (short) nthSample;
                    outcount ++;
                }
            }
            return outcount;
        }

        protected override double Abs(short value)
        {
            return Math.Abs(value);
        }

        protected override short CutPeaks(double value)
        {
            var longValue = (long) (value);

            // cut peaks (shouldn't be necessary though)
            if (longValue > 32767) longValue = 32767;

            return (short) longValue;
        }
    }

    public sealed class BpmDetectFloat : BpmDetect<float, double>
    {
        public BpmDetectFloat(int numChannels, int sampleRate) : base(numChannels, sampleRate)
        {
            // float samples, scaled to range [-1..+1[
            RmsVolumeAccu = (0.092f*0.092f)/Avgnorm;
        }

        protected override void UpdateXCorr(int processSamples)
        {
            Debug.Assert(Buffer.AvailableSamples >= (uint) (processSamples + WindowLen));

            ArrayPtr<float> pBuffer = Buffer.PtrBegin();
            for (int offs = WindowStart; offs < WindowLen; offs ++)
            {
                double sum = 0.0;
                for (int i = 0; i < processSamples; i ++)
                {
                    sum += pBuffer[i]*pBuffer[i + offs]; // scaling the sub-result shouldn't be necessary
                }

                Xcorr[offs] += (float) sum;
            }
        }

        protected override int Decimate(ArrayPtr<float> dst, ArrayPtr<float> src, int numSamples)
        {
            int count;

            Debug.Assert(Channels > 0);
            Debug.Assert(DecimateBy > 0);
            int outcount = 0;
            for (count = 0; count < numSamples; count ++)
            {
                int j;

                // convert to mono and accumulate
                for (j = 0; j < Channels; j ++)
                {
                    DecimateSum += src[j];
                }
                src += j;

                DecimateCount ++;
                if (DecimateCount >= DecimateBy)
                {
                    // Store every Nth sample only
                    double nthSample = (DecimateSum/(DecimateBy*Channels));
                    DecimateSum = 0;
                    DecimateCount = 0;

                    dst[outcount] = (float) nthSample;
                    outcount ++;
                }
            }
            return outcount;
        }

        protected override double Abs(float value)
        {
            return Math.Abs(value);
        }

        protected override float CutPeaks(double value)
        {
            return (float) value;
        }
    }
}