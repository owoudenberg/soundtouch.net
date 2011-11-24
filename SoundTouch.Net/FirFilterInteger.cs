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
    internal sealed class FirFilterInteger : FirFilter<short>
    {

        protected override int EvaluateFilterStereo(ArrayPtr<short> dest, ArrayPtr<short> src, int numSamples)
        {
            Debug.Assert(_length != 0);
            Debug.Assert(src != null);
            Debug.Assert(dest != null);
            Debug.Assert(_filterCoeffs != null);

            int end = 2 * (numSamples - _length);

            for (int j = 0; j < end; j += 2)
            {
                long suml = 0, sumr = 0;
                ArrayPtr<short> ptr = src + j;

                for (int i = 0; i < _length; i += 4)
                {
                    // loop is unrolled by factor of 4 here for efficiency
                    suml += ptr[2 * i + 0] * _filterCoeffs[i + 0] +
                            ptr[2 * i + 2] * _filterCoeffs[i + 1] +
                            ptr[2 * i + 4] * _filterCoeffs[i + 2] +
                            ptr[2 * i + 6] * _filterCoeffs[i + 3];
                    sumr += ptr[2 * i + 1] * _filterCoeffs[i + 0] +
                            ptr[2 * i + 3] * _filterCoeffs[i + 1] +
                            ptr[2 * i + 5] * _filterCoeffs[i + 2] +
                            ptr[2 * i + 7] * _filterCoeffs[i + 3];
                }

                suml >>= _resultDivFactor;
                sumr >>= _resultDivFactor;

                // saturate to 16 bit integer limits
                suml = (suml < short.MinValue) ? short.MinValue : (suml > short.MaxValue) ? short.MaxValue : suml;
                // saturate to 16 bit integer limits
                sumr = (sumr < short.MinValue) ? short.MinValue : (sumr > short.MaxValue) ? short.MaxValue : sumr;

                dest[j] = (short)suml;
                dest[j + 1] = (short)sumr;
            }
            return numSamples - _length;
        }

        protected override int EvaluateFilterMono(ArrayPtr<short> dest, ArrayPtr<short> src, int numSamples)
        {
            Debug.Assert(_length != 0);

            int end = numSamples - _length;
            for (int j = 0; j < end; j++)
            {
                long sum = 0;
                for (int i = 0; i < _length; i += 4)
                {
                    // loop is unrolled by factor of 4 here for efficiency
                    sum += src[i + 0] * _filterCoeffs[i + 0] +
                           src[i + 1] * _filterCoeffs[i + 1] +
                           src[i + 2] * _filterCoeffs[i + 2] +
                           src[i + 3] * _filterCoeffs[i + 3];
                }
                sum >>= _resultDivFactor;
                // saturate to 16 bit integer limits
                sum = (sum < short.MinValue) ? short.MinValue : (sum > short.MaxValue) ? short.MaxValue : sum;
                dest[j] = (short)sum;
                src++;
            }
            return end;
        }
    }
}