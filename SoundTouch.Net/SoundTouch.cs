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

using SoundTouch.Utility;

namespace SoundTouch
{

    /// <summary>
    /// SoundTouch - main class for tempo/pitch/rate adjusting routines.
    /// </summary>
    /// <remarks>
    /// - Initialize the SoundTouch object instance by setting up the sound
    /// stream  parameters with functions <see cref="SetSampleRate"/> and 
    /// <see cref="SetChannels"/>, then set  desired tempo/pitch/rate settings
    /// with the corresponding functions.
    ///
    /// - The SoundTouch class behaves like a first-in-first-out pipeline: The 
    /// samples that are to be processed are fed into one of the pipe by calling
    /// function <see cref="PutSamples"/>, while the ready processed samples can
    /// be read  from the other end of the pipeline with function 
    /// <see cref="FifoProcessor{TSampletype}.ReceiveSamples(SoundTouch.Utility.ArrayPtr{TSampletype},int)"/>
    /// .
    /// 
    /// - The SoundTouch processing classes require certain sized 'batches' of 
    /// samples in order to process the sound. For this reason the classes
    /// buffer  incoming samples until there are enough of samples available for
    /// processing, then they carry out the processing step and consequently
    /// make the processed samples available for outputting.
    /// 
    /// - For the above reason, the processing routines introduce a certain 
    /// 'latency' between the input and output, so that the samples input to
    /// SoundTouch may not be immediately available in the output, and neither 
    /// the amount of outputtable samples may not immediately be in direct 
    /// relationship with the amount of previously input samples.
    ///
    /// - The tempo/pitch/rate control parameters can be altered during
    /// processing. Please notice though that they aren't currently protected by
    /// semaphores, so in multi-thread application external semaphore protection
    /// may be required.
    ///
    /// - This class utilizes classes 
    /// <see cref="TimeStretch{TSampleType,TLongSampleType}"/> for tempo change
    /// (without modifying pitch) and <see cref="RateTransposer{TSampleType}"/>
    /// for changing the playback rate (that is, both  tempo and pitch in the
    /// same ratio) of the sound. The third available control  'pitch' (change
    /// pitch but maintain tempo) is produced by a combination of combining the
    /// two other controls.
    /// </remarks>
    public sealed class SoundTouch<TSampletype, TLongSampleType> : FifoProcessor<TSampletype> 
        where TSampletype : struct
        where TLongSampleType : struct
    {
        /// <summary><c>SoundTouch</c> library version string</summary>
        private const string SOUNDTOUCH_VERSION = "1.6.0";

        /// <summary><c>SoundTouch</c> library version id</summary>
        private const int SOUNDTOUCH_VERSION_ID = (10600);


        /// <summary>Rate transposer class instance</summary>
        private readonly RateTransposer<TSampletype> _rateTransposer;
        /// <summary>Time-stretch class instance</summary>
        private readonly TimeStretch<TSampletype, TLongSampleType> _stretch;

        /// <summary>Flag: Has sample rate been set?</summary>
        private bool _isSampleRateSet;

        /// <summary>Virtual pitch parameter. Effective rate & tempo are calculated from these parameters.</summary>
        private float _virtualPitch;
        /// <summary>Virtual tempo parameter. Effective rate & tempo are calculated from these parameters.</summary>
        private float _virtualTempo;
        /// <summary>Virtual rate parameter. Effective rate & tempo are calculated from these parameters.</summary>
        private float _virtualRate;

        /// <summary>Number of channels</summary>
        private int _channels;
        /// <summary>Effective 'rate' value calculated from <see cref="_virtualRate"/>, <see cref="_virtualTempo"/> and <see cref="_virtualPitch"/></summary>
        private float _rate;
        /// <summary>Effective 'tempo' value calculated from <see cref="_virtualRate"/>, <see cref="_virtualTempo"/> and <see cref="_virtualPitch"/></summary>
        private float _tempo;

        public SoundTouch()
        {
            // Initialize rate transposer and tempo changer instances
            _rateTransposer = RateTransposer<TSampletype>.NewInstance();
            _stretch = TimeStretch<TSampletype, TLongSampleType>.NewInstance();

            SetOutPipe(_stretch);

            _rate = _tempo = 0f;

            _virtualTempo = 
            _virtualRate = 
            _virtualPitch = 1.0f;

            CalcEffectiveRateAndTempo();

            _channels = 0;
            _isSampleRateSet = false;
        }

        /// <summary>Get <c>SoundTouch</c> library version string</summary>
        public static string VersionString
        {
            get { return SOUNDTOUCH_VERSION; }
        }

        /// <summary>Get <c>SoundTouch</c> library version Id</summary>
        public static int VersionId
        {
            get { return SOUNDTOUCH_VERSION_ID; }
        }

        private static bool TestFloatEqual(float a, float b)
        {
            return Math.Abs(a - b) < 1e-10;
        }

        /// <summary>
        /// Calculates effective rate & tempo values from 
        /// <see cref="_virtualRate"/>, <see cref="_virtualTempo"/> and 
        /// <see cref="_virtualPitch"/> parameters.
        /// </summary>
        private void CalcEffectiveRateAndTempo()
        {
            float oldTempo = _tempo;
            float oldRate = _rate;

            _tempo = _virtualTempo / _virtualPitch;
            _rate = _virtualPitch * _virtualRate;

            if (!TestFloatEqual(_rate, oldRate)) _rateTransposer.SetRate(_rate);
            if (!TestFloatEqual(_tempo, oldTempo)) _stretch.SetTempo(_tempo);
            
#if !SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER
            if (_rate <= 1.0)
            {
                if (Output != _stretch)
                {
                    Debug.Assert(Output == _rateTransposer);
                    // move samples in the current output buffer to the output of pTDStretch
                    var tempOut = _stretch.GetOutput();
                    tempOut.MoveSamples(Output);
                    // move samples in pitch transposer's store buffer to tempo changer's input
                    _stretch.MoveSamples(_rateTransposer.GetStore());
                    Output = _stretch;
                }
            }
            else 
#endif                
            if (Output != _rateTransposer)
            {
                Debug.Assert(Output == _stretch);
                // move samples in the current output buffer to the output of pRateTransposer
                var transOut = _rateTransposer.GetOutput();
                transOut.MoveSamples(Output);
                // move samples in tempo changer's input to pitch transposer's input
                _rateTransposer.MoveSamples(_stretch.GetInput());

                Output = _rateTransposer;
            }
        }

        /// <summary>
        /// Clears all the samples in the object's output and internal
        /// processing buffers.
        /// </summary>
        public override void Clear()
        {
            _rateTransposer.Clear();
            _stretch.Clear();
        }

        /// <summary>
        /// Flushes the last samples from the processing pipeline to the output.
        /// Clears also the internal processing buffers.
        ///</summary>
        /// <remarks>
        /// This function is meant for extracting the last samples of a sound
        /// stream. This function may introduce additional blank samples in the
        /// end of the sound stream, and thus it's not recommended to call this
        /// function in the middle of a sound stream.
        /// </remarks>
        public void Flush()
        {
            var buff = new TSampletype[128];
            int nOut = AvailableSamples;
            
            // "Push" the last active samples out from the processing pipeline by
            // feeding blank samples into the processing pipeline until new, 
            // processed samples appear in the output (not however, more than 
            // 8k samples in any case)
            for (int i = 0; i < 128; i ++) 
            {
                PutSamples(buff, 64);
                if (AvailableSamples != nOut) break;  // new samples have appeared in the output!
            }

            // Clear working buffers
            _rateTransposer.Clear();
            _stretch.ClearInput();
            // yet leave the 'tempoChanger' output intouched as that's where the
            // flushed samples are!
        }

        /// <summary>
        /// Reads a setting controlling the processing system behavior. See the
        /// <see cref="SettingId"/> enum for available setting IDs.
        /// </summary>
        /// <param name="settingId">Setting ID number.</param>
        /// <returns>The setting value.</returns>
        public int GetSetting(SettingId settingId)
        {
            int sampleRate, sequenceMs, seekWindowMs, overlapMs;

            _stretch.GetParameters(out sampleRate, out sequenceMs, out seekWindowMs, out overlapMs);                    

            switch (settingId)
            {
                case SettingId.UseAntiAliasFilter:
                    return _rateTransposer.IsAntiAliasFilterEnabled ? 1 : 0;

                case SettingId.AntiAliasFilterLength:
                    return _rateTransposer.GetAntiAliasFilter().GetLength();

                case SettingId.UseQuickseek:
                    return _stretch.IsQuickSeekEnabled ? 1 : 0;

                case SettingId.SequenceDurationMs:
                    return sequenceMs;

                case SettingId.OverlapDurationMs:                    
                    return overlapMs;

                case SettingId.SeekwindowDurationMs:
                    return seekWindowMs;

                case SettingId.NominalInputSequence:
                    return _stretch.GetInputSampleReq();

                case SettingId.NominalOutputSequence:
                    return _stretch.GetOutputBatchSize();
            }
            return 0;
        }

        /// <summary>
        /// Returns number of samples currently unprocessed.
        /// </summary>
        /// <returns></returns>
        public int NumberOfUnprocessedSamples()
        {
            if (_stretch != null)
            {
                var psp = _stretch.GetInput();
                if (psp != null)
                {
                    return psp.AvailableSamples;
                }
            }
            return 0;
        }

        /// <summary>
        /// Adds <paramref name="numSamples"/> pieces of samples from the 
        /// <paramref name="samples"/> memory position into the input of the
        /// object. Notice that sample rate _has_to_ be set before calling this
        /// function, otherwise throws a runtime_error exception.
        /// </summary>
        /// <param name="samples">Pointer to sample buffer.</param>
        /// <param name="numSamples">Number of samples in buffer. Notice that in
        /// case of stereo-sound a single sample contains data for both
        /// channels.</param>
        /// <exception cref="InvalidOperationException">Sample rate or number of
        /// channels not defined</exception>
        public override void PutSamples(ArrayPtr<TSampletype> samples, int numSamples)
        {
            if (!_isSampleRateSet)
                throw new InvalidOperationException("SoundTouch : Sample rate not defined");
            if (_channels == 0)
                throw new InvalidOperationException("SoundTouch : Number of channels not defined");

#if !SOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER
            if (_rate <= 1.0)
            {
                // transpose the rate down, output the transposed sound to tempo changer buffer
                Debug.Assert(Output == _stretch);
                _rateTransposer.PutSamples(samples, numSamples);
                _stretch.MoveSamples(_rateTransposer);
            }
            else
#endif
            {
                // evaluate the tempo changer, then transpose the rate up,
                Debug.Assert(Output == _rateTransposer);
                _stretch.PutSamples(samples, numSamples);
                _rateTransposer.MoveSamples(_stretch);
            }
        }

        /// <summary>
        /// Sets the number of channels, 1 = mono, 2 = stereo
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><c>numChannels</c> is out of range.</exception>
        public void SetChannels(int numChannels)
        {
            if ((numChannels != 1) && (numChannels != 2))
                throw new ArgumentOutOfRangeException("numChannels", numChannels, "Illegal number of channels");

            _channels = numChannels;
            _rateTransposer.SetChannels(numChannels);
            _stretch.SetChannels(numChannels);
        }

        /// <summary>
        /// Sets new pitch control value. Original pitch = 1.0, smaller values
        /// represent lower pitches, larger values higher pitch.
        /// </summary>
        public void SetPitch(float newPitch)
        {
            _virtualPitch = newPitch;
            CalcEffectiveRateAndTempo();
        }

        /// <summary>
        /// Sets pitch change in octaves compared to the original pitch  
        /// (-1.00 .. +1.00)
        /// </summary>
        public void SetPitchOctaves(float newPitch)
        {
            _virtualPitch = (float)Math.Exp(0.69314718056f * newPitch);
            CalcEffectiveRateAndTempo();
        }

        /// <summary>
        /// Sets pitch change in semi-tones compared to the original pitch
        /// (-12 .. +12)
        /// </summary>
        public void SetPitchSemiTones(float newPitch)
        {
            SetPitchOctaves(newPitch / 12.0f);
        }

        /// <summary>
        /// Sets new rate control value. Normal rate = 1.0, smaller values
        /// represent slower rate, larger faster rates.
        /// </summary>
        public void SetRate(float newRate)
        {
            _virtualRate = newRate;
            CalcEffectiveRateAndTempo();
        }

        /// <summary>
        /// Sets new rate control value as a difference in percents compared
        /// to the original rate (-50 .. +100 %)
        /// </summary>
        public void SetRateChange(float newRate)
        {
            _virtualRate = 1.0f + 0.01f * newRate;
            CalcEffectiveRateAndTempo();
        }

        /// <summary>
        /// Sets sample rate.
        /// </summary>
        public void SetSampleRate(int rate)
        {
            _isSampleRateSet = true;
            // set sample rate, leave other tempo changer parameters as they are.
            _stretch.SetParameters(rate);
        }

        /// <summary>
        /// Changes a setting controlling the processing system behavior. See
        /// the <see cref="SettingId"/> enum for available setting IDs.
        /// </summary>
        /// <param name="settingId">Setting ID number.</param>
        /// <param name="value">New setting value.</param>
        /// <returns><c>true</c> if the setting was successfully changed;
        /// otherwise <c>false</c></returns>
        public bool SetSetting(SettingId settingId, int value)
        {
            int sampleRate, sequenceMs, seekWindowMs, overlapMs;

            // read current tdstretch routine parameters
            _stretch.GetParameters(out sampleRate, out sequenceMs, out seekWindowMs, out overlapMs);

            switch (settingId)
            {
                case SettingId.UseAntiAliasFilter:
                    // enables / disabless anti-alias filter
                    _rateTransposer.EnableAntiAliasFilter(value != 0);
                    return true;

                case SettingId.AntiAliasFilterLength:
                    // sets anti-alias filter length
                    _rateTransposer.GetAntiAliasFilter().SetLength(value);
                    return true;

                case SettingId.UseQuickseek:
                    // enables / disables tempo routine quick seeking algorithm
                    _stretch.EnableQuickSeek(value != 0);
                    return true;

                case SettingId.SequenceDurationMs:
                    // change time-stretch sequence duration parameter
                    _stretch.SetParameters(sampleRate, value, seekWindowMs, overlapMs);
                    return true;

                case SettingId.SeekwindowDurationMs:
                    // change time-stretch seek window length parameter
                    _stretch.SetParameters(sampleRate, sequenceMs, value, overlapMs);
                    return true;

                case SettingId.OverlapDurationMs:
                    // change time-stretch overlap length parameter
                    _stretch.SetParameters(sampleRate, sequenceMs, seekWindowMs, value);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Sets new tempo control value. Normal tempo = 1.0, smaller values
        /// represent slower tempo, larger faster tempo.
        /// </summary>
        public void SetTempo(float newTempo)
        {
            _virtualTempo = newTempo;
            CalcEffectiveRateAndTempo();
        }

        /// <summary>
        /// Sets new tempo control value as a difference in percents compared to
        /// the original tempo (-50 .. +100 %)
        /// </summary>
        public void SetTempoChange(float newTempo)
        {
            _virtualTempo = 1.0f + 0.01f * newTempo;
            CalcEffectiveRateAndTempo();
        }
    }
}