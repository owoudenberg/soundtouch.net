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
    /// A common linear sample rate transposer class.
    ///</summary>
    /// <remarks>
    /// Use function <see cref="NewInstance"/> to create a new class  instance
    /// instead of the "new" operator; that function automatically  chooses a
    /// correct implementation depending on if integer or floating  arithmetics
    /// are to be used.
    /// </remarks>
    public abstract class RateTransposer<TSampleType> : FifoProcessor<TSampleType> where TSampleType : struct
    {
        /// <summary>Anti-alias filter object</summary>
        private readonly AntiAliasFilter<TSampleType> _antiAliasFilter;

        protected float Rate;

        private int _channels;

        /// <summary>Buffer for collecting samples to feed the anti-alias filter between two batches</summary>
        private readonly FifoSampleBuffer<TSampleType> _storeBuffer;

        /// <summary>Buffer for keeping samples between transposing & anti-alias filter</summary>
        private readonly FifoSampleBuffer<TSampleType> _tempBuffer;

        /// <summary>Output sample buffer</summary>
        private readonly FifoSampleBuffer<TSampleType> _outputBuffer;

        private bool _useAliasFilter;

        protected RateTransposer()
            : this(new FifoSampleBuffer<TSampleType>())
        {
        }

        private RateTransposer(FifoSampleBuffer<TSampleType> outputBuffer)
            : base(outputBuffer)
        {
            _channels = 2;
            _useAliasFilter = true;
            Rate = 0f;

            _storeBuffer = new FifoSampleBuffer<TSampleType>();
            _tempBuffer = new FifoSampleBuffer<TSampleType>();
            _outputBuffer = outputBuffer;

            _antiAliasFilter = new AntiAliasFilter<TSampleType>(32);
        }

        /// <summary>
        /// Clears all the samples in the object.
        /// </summary>
        public override void Clear()
        {
            _outputBuffer.Clear();
            _storeBuffer.Clear();
        }


        /// <summary>
        /// Transposes down the sample rate, causing the observed playback
        /// 'rate' of the sound to increase
        /// </summary>
        private void Downsample(ArrayPtr<TSampleType> src, int numSamples)
        {
            // If the parameter 'uRate' value is larger than 'SCALE', first apply the
            // anti-alias filter to remove high frequencies (prevent them from folding
            // over the lover frequencies), then transpose.

            // Add the new samples to the end of the storeBuffer
            _storeBuffer.PutSamples(src, numSamples);

            // Anti-alias filter the samples to prevent folding and output the filtered 
            // data to tempBuffer. Note : because of the FIR filter length, the
            // filtering routine takes in 'filter_length' more samples than it outputs.
            Debug.Assert(_tempBuffer.IsEmpty);
            var sizeTemp = _storeBuffer.AvailableSamples;

            int count = _antiAliasFilter.Evaluate(_tempBuffer.PtrEnd(sizeTemp), _storeBuffer.PtrBegin(), sizeTemp, _channels);

            if (count == 0) return;

            // Remove the filtered samples from 'storeBuffer'
            _storeBuffer.ReceiveSamples(count);

            // Transpose the samples (+16 is to reserve some slack in the destination buffer)
            sizeTemp = (int)(numSamples / Rate + 16.0f);
            count = Transpose(_outputBuffer.PtrEnd(sizeTemp), _tempBuffer.PtrBegin(), count);
            _outputBuffer.PutSamples(count);
        }

        /// <summary>
        /// Enables/disables the anti-alias filter. Zero to disable, nonzero to
        /// enable
        /// </summary>
        public void EnableAntiAliasFilter(bool newMode)
        {
            _useAliasFilter = newMode;
        }

        /// <summary>
        /// Returns <c>true</c> if anti-alias filter is enabled.
        /// </summary>
        public bool IsAntiAliasFilterEnabled
        {
            get { return _useAliasFilter; }
        }

        /// <summary>
        /// Return anti-alias filter object
        /// </summary>
        public AntiAliasFilter<TSampleType> GetAntiAliasFilter()
        {
            return _antiAliasFilter;
        }

        /// <summary>
        /// Returns the output buffer object
        /// </summary>
        public FifoSamplePipe<TSampleType> GetOutput()
        {
            return _outputBuffer;
        }

        /// <summary>
        /// Returns the store buffer object
        /// </summary>
        public FifoSamplePipe<TSampleType> GetStore()
        {
            return _storeBuffer;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is empty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is empty; otherwise, <c>false</c>.
        /// </value>
        public override bool IsEmpty
        {
            get { return base.IsEmpty && _storeBuffer.IsEmpty; }
        }

        /// <summary>
        /// Use this function instead of "new" operator to create a new instance
        /// of this class.  This function automatically chooses a correct
        /// implementation, depending on <typeparamref name="TSampleType"/>
        /// integer or floating point arithmetics are to be used.
        /// </summary>
        /// <exception cref="InvalidOperationException">Can't create a 
        /// <see cref="RateTransposer{TSampleType}"/> instance for specified
        /// type. Only <c>short</c> and <c>float</c> are supported.</exception>
        public static RateTransposer<TSampleType> NewInstance()
        {
            if (typeof(TSampleType) == typeof(short))
                return (RateTransposer<TSampleType>)((object)new RateTransposerInteger());
            if (typeof(TSampleType) == typeof(float))
                return (RateTransposer<TSampleType>)((object)new RateTransposerFloat());

            throw new InvalidOperationException(string.Format("Can't create a RateTransposer instance for type {0}. Only <short> and <float> are supported.", typeof(TSampleType)));
        }

        /// <summary>
        /// Transposes sample rate by applying anti-alias filter to prevent
        /// folding.  Returns amount of samples returned in the 
        /// <see cref="_outputBuffer"/> buffer.
        /// </summary>
        private void ProcessSamples(ArrayPtr<TSampleType> src, int numSamples)
        {
            if (numSamples == 0) return;
            Debug.Assert(_antiAliasFilter != null);

            // If anti-alias filter is turned off, simply transpose without applying
            // the filter
            if (!_useAliasFilter)
            {
                var sizeReq = (int)(numSamples / Rate + 1.0f);
                int count = Transpose(_outputBuffer.PtrEnd(sizeReq), src, numSamples);
                _outputBuffer.PutSamples(count);
                return;
            }

            // Transpose with anti-alias filter
            if (Rate < 1.0f)
                Upsample(src, numSamples);
            else
                Downsample(src, numSamples);
        }

        /// <summary>
        /// Adds <paramref name="numSamples"/> pcs of samples from the 
        /// <paramref name="samples"/> memory position into the input of the
        /// object.
        /// </summary>
        public override void PutSamples(ArrayPtr<TSampleType> samples, int numSamples)
        {
            ProcessSamples(samples, numSamples);
        }

        protected abstract void ResetRegisters();

        /// <summary>
        /// Sets the number of channels, 1 = mono, 2 = stereo
        /// </summary>
        public void SetChannels(int channels)
        {
            Debug.Assert(channels > 0);
            if (_channels == channels) return;

            Debug.Assert((channels == 1) || (channels == 2));
            _channels = channels;

            _storeBuffer.SetChannels(_channels);
            _tempBuffer.SetChannels(_channels);
            _outputBuffer.SetChannels(_channels);

            // Inits the linear interpolation registers
            ResetRegisters();
        }

        /// <summary>
        /// Sets new target rate. Normal rate = 1.0, smaller values represent
        /// slower  rate, larger faster rates.
        /// </summary>
        public virtual void SetRate(float newRate)
        {
            double cutoff;

            Rate = newRate;

            // design a new anti-alias filter
            if (newRate > 1.0f)
                cutoff = 0.5f / newRate;
            else
                cutoff = 0.5f * newRate;

            _antiAliasFilter.SetCutoffFreq(cutoff);
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear
        /// interpolation. 
        /// </summary>
        /// <returns>Returns the number of samples returned in the 
        /// <paramref name="dest"/> buffer.</returns>
        private int Transpose(ArrayPtr<TSampleType> dest, ArrayPtr<TSampleType> src, int numSamples)
        {
            if (_channels == 2)
                return TransposeStereo(dest, src, numSamples);
            return TransposeMono(dest, src, numSamples);
        }

        protected abstract int TransposeMono(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples);
        protected abstract int TransposeStereo(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples);

        /// <summary>
        /// Transposes up the sample rate, causing the observed playback 'rate'
        /// of the sound to decrease
        /// </summary>
        private void Upsample(ArrayPtr<TSampleType> src, int numSamples)
        {
            // If the parameter 'uRate' value is smaller than 'SCALE', first transpose
            // the samples and then apply the anti-alias filter to remove aliasing.

            // First check that there's enough room in 'storeBuffer' 
            // (+16 is to reserve some slack in the destination buffer)
            var sizeTemp = (int)(numSamples / Rate + 16.0f);

            // Transpose the samples, store the result into the end of "storeBuffer"
            var count = Transpose(_storeBuffer.PtrEnd(sizeTemp), src, numSamples);
            _storeBuffer.PutSamples(count);

            // Apply the anti-alias filter to samples in "store output", output the
            // result to "dst"
            int num = _storeBuffer.AvailableSamples;
            count = _antiAliasFilter.Evaluate(_outputBuffer.PtrEnd(num), _storeBuffer.PtrBegin(), num, _channels);
            _outputBuffer.PutSamples(count);

            // Remove the processed samples from "storeBuffer"
            _storeBuffer.ReceiveSamples(count);
        }
    }
}