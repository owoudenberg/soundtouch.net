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
    internal sealed class TimeStretchFloat : TimeStretch<float, double>
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
                float temp = i*(float) (_overlapLength - i);
                int cnt2 = i*2;
                _refMidBuffer[cnt2] = _midBuffer[cnt2]*temp;
                _refMidBuffer[cnt2 + 1] = _midBuffer[cnt2 + 1]*temp;
            }
        }


        /// <summary>
        /// Slopes the amplitude of the 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}._midBuffer"/> 
        /// samples so that cross correlation is faster to calculate
        /// </summary>
        protected override void PrecalcCorrReferenceMono()
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                float temp = i*(float) (_overlapLength - i);
                _refMidBuffer[i] = _midBuffer[i]*temp;
            }
        }

        protected override void OverlapMono(ArrayPtr<float> pOutput, ArrayPtr<float> pInput)
        {
            for (int i = 0; i < _overlapLength; i++)
            {
                int itemp = _overlapLength - i;
                pOutput[i] = (pInput[i]*i + _midBuffer[i]*itemp)/_overlapLength;
            }
        }


        /// <summary>
        /// Overlaps samples in 
        /// <see cref="TimeStretch{TSampleType,TLongSampleType}._midBuffer"/>
        /// with the  samples in <paramref name="pInput"/>
        /// </summary>
        protected override void OverlapStereo(ArrayPtr<float> pOutput, ArrayPtr<float> pInput)
        {
            float fScale = 1.0f/_overlapLength;

            for (int i = 0; i < _overlapLength; i++)
            {
                float fTemp = (_overlapLength - i)*fScale;
                float fi = i*fScale;
                int cnt2 = 2*i;
                pOutput[cnt2 + 0] = pInput[cnt2 + 0]*fi + _midBuffer[cnt2 + 0]*fTemp;
                pOutput[cnt2 + 1] = pInput[cnt2 + 1]*fi + _midBuffer[cnt2 + 1]*fTemp;
            }
        }


        /// <summary>Calculates <paramref name="overlapInMsec"/> period length
        /// in samples.</summary>
        protected override void CalculateOverlapLength(int overlapInMsec)
        {
            Debug.Assert(overlapInMsec >= 0);
            int newOvl = (_sampleRate*overlapInMsec)/1000;
            if (newOvl < 16) newOvl = 16;

            // must be divisible by 8
            newOvl -= newOvl%8;

            AcceptNewOverlapLength(newOvl);
        }

        protected override double CalculateCrossCorrMono(ArrayPtr<float> mixingPos, ArrayPtr<float> compare)
        {
            double corr = 0, norm = 0;

            for (int i = 1; i < _overlapLength; i++)
            {
                corr += mixingPos[i]*compare[i];
                norm += mixingPos[i]*mixingPos[i];
            }

            if (norm < 1e-9) norm = 1.0; // to avoid div by zero
            return corr/Math.Sqrt(norm);
        }


        protected override double CalculateCrossCorrStereo(ArrayPtr<float> mixingPos, ArrayPtr<float> compare)
        {
            double norm = 0;
            double corr = 0;

            for (int i = 2; i < 2*_overlapLength; i += 2)
            {
                corr += mixingPos[i]*compare[i] +
                        mixingPos[i + 1]*compare[i + 1];
                norm += mixingPos[i]*mixingPos[i] +
                        mixingPos[i + 1]*mixingPos[i + 1];
            }

            if (norm < 1e-9) norm = 1.0; // to avoid div by zero
            return corr/Math.Sqrt(norm);
        }
    }
}