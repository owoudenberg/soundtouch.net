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
        /// <summary>
        /// Slopes the amplitude of the 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}._midBuffer"/> 
        /// samples so that cross correlation is faster to calculate
        /// </summary>
        protected override void PrecalcCorrReferenceStereo()
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                int temp = i*(_overlapLength - i);
                int cnt2 = i*2;

                int temp2 = (_midBuffer[cnt2]*temp)/_slopingDivider;
                _refMidBuffer[cnt2] = (short) (temp2);
                temp2 = (_midBuffer[cnt2 + 1]*temp)/_slopingDivider;
                _refMidBuffer[cnt2 + 1] = (short) (temp2);
            }
        }


        /// <summary>Slopes the amplitude of the 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}._midBuffer"/>
        /// samples so that cross correlation is faster to calculate</summary>
        protected override void PrecalcCorrReferenceMono()
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                long temp = i*(_overlapLength - i);
                long temp2 = (_midBuffer[i]*temp)/_slopingDivider;
                _refMidBuffer[i] = (short) temp2;
            }
        }

        protected override void OverlapMono(ArrayPtr<short> pOutput, ArrayPtr<short> pInput)
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                int itemp = _overlapLength - i;
                pOutput[i] = (short) ((pInput[i]*i + _midBuffer[i]*itemp)/_overlapLength);
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
                var temp = (short) (_overlapLength - i);
                int cnt2 = 2*i;
                poutput[cnt2] = (short) ((input[cnt2]*i + _midBuffer[cnt2]*temp)/_overlapLength);
                poutput[cnt2 + 1] = (short) ((input[cnt2 + 1]*i + _midBuffer[cnt2 + 1]*temp)/_overlapLength);
            }
        }

        /// <summary>Calculates the x having the closest 2^x value for the given value</summary>
        private static int GetClosest2Power(double value)
        {
            return (int) (Math.Log(value)/Math.Log(2.0) + 0.5);
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
            _overlapDividerBits = GetClosest2Power((_sampleRate*aoverlapMs)/1000.0) - 1;
            if (_overlapDividerBits > 9) _overlapDividerBits = 9;
            if (_overlapDividerBits < 3) _overlapDividerBits = 3;
            var newOvl = (int) Math.Pow(2.0, _overlapDividerBits + 1);

            AcceptNewOverlapLength(newOvl);

            // calculate sloping divider so that crosscorrelation operation won't 
            // overflow 32-bit register. Max. sum of the crosscorrelation sum without 
            // divider would be 2^30*(N^3-N)/3, where N = overlap length
            _slopingDivider = (newOvl*newOvl - 1)/3;
        }


        protected override long CalculateCrossCorrMono(ArrayPtr<short> mixingPos, ArrayPtr<short> compare)
        {
            long corr = 0, norm = 0;
            for (int i = 1; i < _overlapLength; i++)
            {
                corr += (mixingPos[i]*compare[i]) >> _overlapDividerBits;
                norm += (mixingPos[i]*mixingPos[i]) >> _overlapDividerBits;
            }

            // Normalize result by dividing by sqrt(norm) - this step is easiest 
            // done using floating point operation
            if (norm == 0) norm = 1; // to avoid div by zero
            return (long) ((double) corr*short.MaxValue/Math.Sqrt(norm));
        }


        protected override long CalculateCrossCorrStereo(ArrayPtr<short> mixingPos, ArrayPtr<short> compare)
        {
            long corr = 0, norm = 0;
            for (int i = 2; i < 2*_overlapLength; i += 2)
            {
                corr += (mixingPos[i]*compare[i] +
                         mixingPos[i + 1]*compare[i + 1]) >> _overlapDividerBits;
                norm += (mixingPos[i]*mixingPos[i] + mixingPos[i + 1]*mixingPos[i + 1]) >> _overlapDividerBits;
            }

            // Normalize result by dividing by sqrt(norm) - this step is easiest 
            // done using floating point operation
            if (norm == 0) norm = 1; // to avoid div by zero
            return (long) ((double) corr*short.MaxValue/Math.Sqrt(norm));
        }
    }
}