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
using System.Threading.Tasks;
using SoundTouch.Utility;

namespace SoundTouch
{
    internal sealed class FirFilterFloat : FirFilter<float>
    {
        private double _resultDivider;

        public FirFilterFloat()
        {
            _resultDivider = 0;
        }

        protected override int EvaluateFilterStereo(ArrayPtr<float> dest, ArrayPtr<float> src, int numSamples)
        {
            double dScaler = 1.0 / _resultDivider;

            Debug.Assert(_length != 0);
            Debug.Assert(src != null);
            Debug.Assert(dest != null);
            Debug.Assert(_filterCoeffs != null);

            int end = 2 * (numSamples - _length);

            Parallel.ForEach(RangeEnumerable.Range(0, end, 2), (int j) =>
            {
                double sumr = 0, suml = 0;
                ArrayPtr<float> ptr = src + j;

                for (int i = 0; i < _length; i += 4)
                {
                    // loop is unrolled by factor of 4 here for efficiency
                    suml += ptr[2*i + 0]*_filterCoeffs[i + 0] +
                            ptr[2*i + 2]*_filterCoeffs[i + 1] +
                            ptr[2*i + 4]*_filterCoeffs[i + 2] +
                            ptr[2*i + 6]*_filterCoeffs[i + 3];
                    sumr += ptr[2*i + 1]*_filterCoeffs[i + 0] +
                            ptr[2*i + 3]*_filterCoeffs[i + 1] +
                            ptr[2*i + 5]*_filterCoeffs[i + 2] +
                            ptr[2*i + 7]*_filterCoeffs[i + 3];
                }

                suml *= dScaler;
                sumr *= dScaler;

                dest[j] = (float) suml;
                dest[j + 1] = (float) sumr;
            });
            return numSamples - _length;
        }

        protected override int EvaluateFilterMono(ArrayPtr<float> dest, ArrayPtr<float> src, int numSamples)
        {
            double dScaler = 1.0 / _resultDivider;

            Debug.Assert(_length != 0);

            int end = numSamples - _length;
            Parallel.For(0, end, (int j) =>
            {
                ArrayPtr<float> pSrc = src + j;
                double sum = 0;
                for (int i = 0; i < _length; i += 4)
                {
                    // loop is unrolled by factor of 4 here for efficiency
                    sum += pSrc[i + 0]*_filterCoeffs[i + 0] +
                           pSrc[i + 1]*_filterCoeffs[i + 1] +
                           pSrc[i + 2]*_filterCoeffs[i + 2] +
                           pSrc[i + 3]*_filterCoeffs[i + 3];
                }
                sum *= dScaler;

                dest[j] = (float) sum;
            });
            return end;
        }

        protected override int EvaluateFilterMulti(ArrayPtr<float> dest, ArrayPtr<float> src, int numSamples, int numChannels)
        {
            // when using floating point samples, use a scaler instead of a divider
            // because division is much slower operation than multiplying.
            double dScaler = 1.0 / _resultDivider;

            Debug.Assert(_length != 0);
            Debug.Assert(src != null);
            Debug.Assert(dest != null);
            Debug.Assert(_filterCoeffs != null);
            Debug.Assert(numChannels < 16);

            var end = numChannels * (numSamples - _length);


            Parallel.ForEach(RangeEnumerable.Range(0, end, numChannels), (int j) =>
            {
                double[] sums = new double[16];
                uint c, i;

                for (c = 0; c < numChannels; c++)
                {
                    sums[c] = 0;
                }

                var ptr = src + j;

                for (i = 0; i < _length; i++)
                {
                    float coef = _filterCoeffs[i];
                    for (c = 0; c < numChannels; c++)
                    {
                        sums[c] += ptr[0]*coef;
                        ptr++;
                    }
                }

                for (c = 0; c < numChannels; c++)
                {
                    sums[c] *= dScaler;
                    dest[j + c] = (float) sums[c];
                }
            });

            return numSamples - _length;
        }

        public override void SetCoefficients(ArrayPtr<float> coeffs, int newLength, int resultDivFactor)
        {
            base.SetCoefficients(coeffs, newLength, resultDivFactor);
            _resultDivider = Math.Pow(2.0, _resultDivFactor);
        }
    }
}