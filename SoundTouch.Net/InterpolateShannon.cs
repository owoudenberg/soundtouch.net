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
    public sealed class InterpolateShannon : TransposerBaseFloat
    {
        /// Kaiser window with beta = 2.0
        /// Values scaled down by 5% to avoid overflows
        private static readonly double[] _kaiser8 =
        {
            0.41778693317814,
            0.64888025049173,
            0.83508562409944,
            0.93887857733412,
            0.93887857733412,
            0.83508562409944,
            0.64888025049173,
            0.41778693317814
        };

        private double _fract;

        private double sinc(double x) => Math.Sin(Math.PI * x) / (Math.PI * x);

        public InterpolateShannon()
        {
            _fract = 0;
        }

        protected override void ResetRegisters()
        {
            _fract = 0;
        }

        protected override int TransposeMono(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 8;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_fract < 1.0);

                var @out = dst[0] * sinc(-3.0 - _fract) * _kaiser8[0];
                @out += dst[1] * sinc(-2.0 - _fract) * _kaiser8[1];
                @out += dst[2] * sinc(-1.0 - _fract) * _kaiser8[2];
                if (_fract < 1e-6)
                {
                    @out += src[3] * _kaiser8[3];     // sinc(0) = 1
                }
                else
                {
                    @out += src[3] * sinc(-_fract) * _kaiser8[3];
                }
                @out += src[4] * sinc(1.0 - _fract) * _kaiser8[4];
                @out += src[5] * sinc(2.0 - _fract) * _kaiser8[5];
                @out += src[6] * sinc(3.0 - _fract) * _kaiser8[6];
                @out += src[7] * sinc(4.0 - _fract) * _kaiser8[7];

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

        protected override int TransposeStereo(ArrayPtr<float> dst, ArrayPtr<float> src, ref int srcSamples)
        {
            int srcSampleEnd = srcSamples - 8;
            int srcCount = 0;

            var i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_fract < 1.0);

                var w = sinc(-3.0 - _fract) * _kaiser8[0];
                var out0 = src[0] * w; var out1 = src[1] * w;
                w = sinc(-2.0 - _fract) * _kaiser8[1];
                out0 += src[2] * w; out1 += src[3] * w;
                w = sinc(-1.0 - _fract) * _kaiser8[2];
                out0 += src[4] * w; out1 += src[5] * w;
                w = _kaiser8[3] * ((_fract < 1e-5) ? 1.0 : sinc(-_fract));   // sinc(0) = 1
                out0 += src[6] * w; out1 += src[7] * w;
                w = sinc(1.0 - _fract) * _kaiser8[4];
                out0 += src[8] * w; out1 += src[9] * w;
                w = sinc(2.0 - _fract) * _kaiser8[5];
                out0 += src[10] * w; out1 += src[11] * w;
                w = sinc(3.0 - _fract) * _kaiser8[6];
                out0 += src[12] * w; out1 += src[13] * w;
                w = sinc(4.0 - _fract) * _kaiser8[7];
                out0 += src[14] * w; out1 += src[15] * w;

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
            throw new NotImplementedException();
        }
    }
}