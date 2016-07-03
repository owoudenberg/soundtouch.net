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
    public sealed class InterpolateLinearInteger : TransposerBaseInteger
    {
        private const int SCALE = 65536;

        private int _iFract;
        private int _iRate;

        public InterpolateLinearInteger()
        {
            // Notice: use local function calling syntax for sake of clarity, 
            // to indicate the fact that C++ constructor can't call virtual functions.
            ResetRegisters();
            SetRate(1.0f);
        }

        /// <summary>
        /// Sets new target rate. Normal rate = 1.0, smaller values represent slower 
        /// rate, larger faster rates.
        /// </summary>
        public override void SetRate(double newRate)
        {
            _iRate = (int)(newRate * SCALE + 0.5);
            base.SetRate(newRate);
        }

        protected override void ResetRegisters()
        {
            _iFract = 0;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear interpolation. 
        /// 'Mono' version of the routine. Returns the number of samples returned in 
        /// the "dest" buffer
        /// </summary>
        protected override int TransposeMono(ArrayPtr<short> dst, ArrayPtr<short> src, ref int srcSamples)
        {
            int i;
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_iFract < SCALE);

                long temp = (SCALE - _iFract) * src[0] + _iFract * src[1];
                dst[i] = (short) (temp / SCALE);
                i++;

                _iFract += _iRate;

                int iWhole = _iFract / SCALE;
                _iFract -= iWhole * SCALE;
                srcCount += iWhole;
                src += iWhole;
            }
            srcSamples = srcCount;

            return i;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear interpolation. 
        /// 'Stereo' version of the routine. Returns the number of samples returned in 
        /// the "dest" buffer
        /// </summary>
        protected override int TransposeStereo(ArrayPtr<short> dst, ArrayPtr<short> src, ref int srcSamples)
        {
            int i;
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            i = 0;
            while (srcCount < srcSampleEnd)
            {
                Debug.Assert(_iFract < SCALE);

                long temp0 = (SCALE - _iFract) * src[0] + _iFract * src[2];
                long temp1 = (SCALE - _iFract) * src[1] + _iFract * src[3];
                dst[0] = (short)(temp0 / SCALE);
                dst[1] = (short)(temp1 / SCALE);
                dst += 2;
                i++;

                _iFract += _iRate;

                int iWhole = _iFract / SCALE;
                _iFract -= iWhole * SCALE;
                srcCount += iWhole;
                src += 2 * iWhole;
            }
            srcSamples = srcCount;

            return i;
        }

        protected override int TransposeMulti(ArrayPtr<short> dst, ArrayPtr<short> src, ref int srcSamples)
        {
            int i;
            int srcSampleEnd = srcSamples - 1;
            int srcCount = 0;

            i = 0;
            while (srcCount < srcSampleEnd)
            {
                long temp, vol1;

                Debug.Assert(_iFract < SCALE);
                vol1 = SCALE - _iFract;
                for (int c = 0; c < channels; c++)
                {
                    temp = vol1 * src[c] + _iFract * src[c + channels];
                    dst[0] = (short)(temp / SCALE);
                    dst++;
                }
                i++;

                _iFract += _iRate;

                int iWhole = _iFract / SCALE;
                _iFract -= iWhole * SCALE;
                srcCount += iWhole;
                src += iWhole * channels;
            }
            srcSamples = srcCount;

            return i;
        }
    }
}