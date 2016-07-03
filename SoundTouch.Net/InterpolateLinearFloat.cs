using System.Diagnostics;
using SoundTouch.Utility;

namespace SoundTouch
{
    public sealed class InterpolateLinearFloat : TransposerBaseFloat
    {
        private double _fract;

        public InterpolateLinearFloat()
        {
            ResetRegisters();
            SetRate(1.0);
        }

        protected override void ResetRegisters()
        {
            _fract = 0;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear interpolation. 
        /// 'Mono' version of the routine. Returns the number of samples returned in 
        /// the "dest" buffer
        /// </summary>
        protected override int TransposeMono(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_fract < 1.0);

                var @out = (1.0 - _fract) * src[0] + _fract * src[1];
                dst[i] = (float)@out;
                i++;

                // update position fraction
                _fract += rate;
                // update whole positions
                int whole = (int)_fract;
                _fract -= whole;
                src += whole;
                srcCount += whole;
            }
            srcSamples = srcCount;
            return i;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear interpolation. 
        /// 'Mono' version of the routine. Returns the number of samples returned in 
        /// the "dest" buffer
        /// </summary>
        protected override int TransposeStereo(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_fract < 1.0);

                var out0 = (1.0 - _fract) * src[0] + _fract * src[2];
                var out1 = (1.0 - _fract) * src[1] + _fract * src[3];
                dst[2 * i] = (float)out0;
                dst[2 * i + 1] = (float)out1;
                i++;

                // update position fraction
                _fract += rate;
                // update whole positions
                int whole = (int)_fract;
                _fract -= whole;
                src += 2 * whole;
                srcCount += whole;
            }
            srcSamples = srcCount;
            return i;
        }

        protected override int TransposeMulti(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                var vol1 = (float)(1.0 - _fract);
                var fractFloat = (float)_fract;
                for (int c = 0; c < channels; c++)
                {
                    var temp = vol1 * src[c] + fractFloat * src[c + channels];
                    dst[0] = temp;
                    dst++;
                }
                i++;

                _fract += rate;

                int iWhole = (int)_fract;
                _fract -= iWhole;
                srcCount += iWhole;
                src += iWhole * channels;
            }
            srcSamples = srcCount;

            return i;
        }
    }
}