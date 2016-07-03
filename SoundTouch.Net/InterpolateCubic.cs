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

using System.Diagnostics;
using SoundTouch.Utility;

namespace SoundTouch
{
    public sealed class InterpolateCubic : TransposerBaseFloat
    {
        private static readonly float[] _coeffs =
        {
            -0.5f, 1.0f, -0.5f, 0.0f,
            1.5f, -2.5f, 0.0f, 1.0f,
            -1.5f, 2.0f, 0.5f, 0.0f,
            0.5f, -0.5f, 0.0f, 0.0f
        };

        private double _fract;

        public InterpolateCubic()
        {
            _fract = 0;
        }

        protected override void ResetRegisters()
        {
            _fract = 0;
        }

        /// <summary>
        /// Transpose mono audio. Returns number of produced output samples, and 
        /// updates "srcSamples" to amount of consumed source samples
        /// </summary>
        protected override int TransposeMono(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 4;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                float x3 = 1.0f;
                float x2 = (float)_fract;    // x
                float x1 = x2 * x2;           // x^2
                float x0 = x1 * x2;           // x^3

                Debug.Assert(_fract < 1.0);

                var y0 = _coeffs[0] * x0 + _coeffs[1] * x1 + _coeffs[2] * x2 + _coeffs[3] * x3;
                var y1 = _coeffs[4] * x0 + _coeffs[5] * x1 + _coeffs[6] * x2 + _coeffs[7] * x3;
                var y2 = _coeffs[8] * x0 + _coeffs[9] * x1 + _coeffs[10] * x2 + _coeffs[11] * x3;
                var y3 = _coeffs[12] * x0 + _coeffs[13] * x1 + _coeffs[14] * x2 + _coeffs[15] * x3;

                var @out = y0 * src[0] + y1 * src[1] + y2 * src[2] + y3 * src[3];

                dst[i] = @out;
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
        /// Transpose stereo audio. Returns number of produced output samples, and 
        /// updates "srcSamples" to amount of consumed source samples
        /// </summary>
        protected override int TransposeStereo(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 4;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                float x3 = 1.0f;
                float x2 = (float)_fract;    // x
                float x1 = x2 * x2;           // x^2
                float x0 = x1 * x2;           // x^3

                Debug.Assert(_fract < 1.0);

                var y0 = _coeffs[0] * x0 + _coeffs[1] * x1 + _coeffs[2] * x2 + _coeffs[3] * x3;
                var y1 = _coeffs[4] * x0 + _coeffs[5] * x1 + _coeffs[6] * x2 + _coeffs[7] * x3;
                var y2 = _coeffs[8] * x0 + _coeffs[9] * x1 + _coeffs[10] * x2 + _coeffs[11] * x3;
                var y3 = _coeffs[12] * x0 + _coeffs[13] * x1 + _coeffs[14] * x2 + _coeffs[15] * x3;

                var out0 = y0 * src[0] + y1 * src[2] + y2 * src[4] + y3 * src[6];
                var out1 = y0 * src[1] + y1 * src[3] + y2 * src[5] + y3 * src[7];

                dst[2*i] = out0;
                dst[2 * i + 1] = out1;
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
            int srcSampleEnd = srcSamples - 4;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                float x3 = 1.0f;
                float x2 = (float)_fract;    // x
                float x1 = x2 * x2;           // x^2
                float x0 = x1 * x2;           // x^3

                Debug.Assert(_fract < 1.0);

                var y0 = _coeffs[0] * x0 + _coeffs[1] * x1 + _coeffs[2] * x2 + _coeffs[3] * x3;
                var y1 = _coeffs[4] * x0 + _coeffs[5] * x1 + _coeffs[6] * x2 + _coeffs[7] * x3;
                var y2 = _coeffs[8] * x0 + _coeffs[9] * x1 + _coeffs[10] * x2 + _coeffs[11] * x3;
                var y3 = _coeffs[12] * x0 + _coeffs[13] * x1 + _coeffs[14] * x2 + _coeffs[15] * x3;

                for (int c = 0; c < srcSamples; c++)
                {
                    float @out = y0 * src[c] + y1 * src[c + channels] + y2 * src[c + 2 * channels] + y3 * src[c + 3 * channels];
                    dst[0] = @out;
                    dst++;
                }
                i++;

                // update position fraction
                _fract += rate;
                // update whole positions
                int whole = (int)_fract;
                _fract -= whole;
                src += channels * whole;
                srcCount += whole;
            }
            srcSamples = srcCount;
            return i;
        }
    }
}