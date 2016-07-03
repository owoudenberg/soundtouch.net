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
    internal sealed class TimeStretchInteger : TimeStretch<short, long>
    {
        protected override void OverlapMono(ArrayPtr<short> pOutput, ArrayPtr<short> pInput)
        {
            int m1 = 0;
            int m2 = _overlapLength;

            for (int i = 0; i < _overlapLength; i++)
            {
                pOutput[i] = (short)((pInput[i] * m1 + _midBuffer[i] * m2) / _overlapLength);
                m1 += 1;
                m2 -= 1;
            }
        }

        /// <summary>Overlaps samples in 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}._midBuffer"/> 
        /// with the samples in <paramref name="input"/>. The 'Stereo' version
        /// of the routine.</summary>
        protected override void OverlapStereo(ArrayPtr<short> poutput, ArrayPtr<short> input)
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                var temp = (short)(_overlapLength - i);
                int cnt2 = 2 * i;
                poutput[cnt2] = (short)((input[cnt2] * i + _midBuffer[cnt2] * temp) / _overlapLength);
                poutput[cnt2 + 1] = (short)((input[cnt2 + 1] * i + _midBuffer[cnt2 + 1] * temp) / _overlapLength);
            }
        }

        protected override void OverlapMulti(ArrayPtr<short> output, ArrayPtr<short> input)
        {
            short m1 = 0;
            short m2;
            int i = 0;

            for (m2 = (short)_overlapLength; m2 > 0; m2--)
            {
                for (int c = 0; c < _channels; c++)
                {
                    output[i] = (short)((input[i] * m1 + _midBuffer[i] * m2) / _overlapLength);
                    i++;
                }

                m1++;
            }
        }

        /// For integer algorithm: adapt normalization factor divider with music so that 
        /// it'll not be pessimistically restrictive that can degrade quality on quieter sections
        /// yet won't cause integer overflows either
        protected override void AdaptNormalizer()
        {
            // Do not adapt normalizer over too silent sequences to avoid averaging filter depleting to
            // too low values during pauses in music
            if ((_maxNorm > 1000) || (_maxNormFloat > 40000000))
            {
                //norm averaging filter
                _maxNormFloat = 0.9f * _maxNormFloat + 0.1f * (float)_maxNorm;

                if ((_maxNorm > 800000000) && (_overlapDividerBitsNorm < 16))
                {
                    // large values, so increase divider
                    _overlapDividerBitsNorm++;
                    if (_maxNorm > 1600000000) _overlapDividerBitsNorm++; // extra large value => extra increase
                }
                else if ((_maxNormFloat < 1000000) && (_overlapDividerBitsNorm > 0))
                {
                    // extra small values, decrease divider
                    _overlapDividerBitsNorm--;
                }
            }

            _maxNorm = 0;
        }

        /// <summary>Calculates the x having the closest 2^x value for the given value</summary>
        private static int GetClosest2Power(double value)
        {
            return (int)(Math.Log(value) / Math.Log(2.0) + 0.5);
        }


        /// <summary>Calculates overlap period length in samples. Integer
        /// version rounds overlap length to closest power of 2 for a divide
        /// scaling operation.</summary>
        protected override void CalculateOverlapLength(int aoverlapMs)
        {
            Debug.Assert(aoverlapMs >= 0);

            // calculate overlap length so that it's power of 2 - thus it's easy to do
            // integer division by right-shifting. Term "-1" at end is to account for 
            // the extra most significatnt bit left unused in result by signed multiplication 
            _overlapDividerBitsPure = GetClosest2Power((_sampleRate * aoverlapMs) / 1000.0) - 1;
            if (_overlapDividerBitsPure > 9) _overlapDividerBitsPure = 9;
            if (_overlapDividerBitsPure < 3) _overlapDividerBitsPure = 3;
            var newOvl = (int)Math.Pow(2.0, _overlapDividerBitsPure + 1);

            AcceptNewOverlapLength(newOvl);
            _overlapDividerBitsNorm = _overlapDividerBitsPure;

            // calculate sloping divider so that crosscorrelation operation won't 
            // overflow 32-bit register. Max. sum of the crosscorrelation sum without 
            // divider would be 2^30*(N^3-N)/3, where N = overlap length
            _slopingDivider = (newOvl * newOvl - 1) / 3;
        }


        protected override double CalculateCrossCorr(ArrayPtr<short> mixingPos, ArrayPtr<short> compare, out double norm)
        {
            long corr = 0;
            long lnorm = 0;

            // Same routine for stereo and mono. For stereo, unroll loop for better
            // efficiency and gives slightly better resolution against rounding. 
            // For mono it same routine, just  unrolls loop by factor of 4
            for (int i = 0; i < _channels * _overlapLength; i += 4)
            {
                corr += (mixingPos[i] * compare[i] +
                         mixingPos[i + 1] * compare[i + 1]) >> _overlapDividerBitsNorm;  // notice: do intermediate division here to avoid integer overflow
                corr += (mixingPos[i + 2] * compare[i + 2] +
                        mixingPos[i + 3] * compare[i + 3]) >> _overlapDividerBitsNorm;
                lnorm += (mixingPos[i] * mixingPos[i] +
                        mixingPos[i + 1] * mixingPos[i + 1]) >> _overlapDividerBitsNorm; // notice: do intermediate division here to avoid integer overflow
                lnorm += (mixingPos[i + 2] * mixingPos[i + 2] +
                        mixingPos[i + 3] * mixingPos[i + 3]) >> _overlapDividerBitsNorm;
            }

            if (lnorm > _maxNorm)
                _maxNorm = lnorm;

            // Normalize result by dividing by sqrt(norm) - this step is easiest 
            // done using floating point operation
            norm = lnorm;
            return corr / Math.Sqrt((norm < 1e-9) ? 1.0 : norm);
        }

        /// Update cross-correlation by accumulating "norm" coefficient by previously calculated value
        protected override double CalculateCrossCorrAccumulate(ArrayPtr<short> mixingPos, ArrayPtr<short> compare, ref double norm)
        {
            long corr;
            long lnorm;
            int i;

            // cancel first normalizer tap from previous round
            lnorm = 0;
            for (i = 1; i <= _channels; i++)
            {
                lnorm -= (mixingPos[-i] * mixingPos[-i]) >> _overlapDividerBitsNorm;
            }

            corr = 0;
            // Same routine for stereo and mono. For stereo, unroll loop for better
            // efficiency and gives slightly better resolution against rounding. 
            // For mono it same routine, just  unrolls loop by factor of 4
            for (i = 0; i < _channels * _overlapLength; i += 4)
            {
                corr += (mixingPos[i] * compare[i] +
                         mixingPos[i + 1] * compare[i + 1]) >> _overlapDividerBitsNorm;  // notice: do intermediate division here to avoid integer overflow
                corr += (mixingPos[i + 2] * compare[i + 2] +
                         mixingPos[i + 3] * compare[i + 3]) >> _overlapDividerBitsNorm;
            }

            // update normalizer with last samples of this round
            for (int j = 0; j < _channels; j++)
            {
                i--;
                lnorm += (mixingPos[i] * mixingPos[i]) >> _overlapDividerBitsNorm;
            }

            norm += lnorm;
            if (norm > _maxNorm)
            {
                _maxNorm = (long) norm;
            }

            // Normalize result by dividing by sqrt(norm) - this step is easiest 
            // done using floating point operation
            return corr / Math.Sqrt((norm < 1e-9) ? 1.0 : norm);
        }
    }
}