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
        protected override void OverlapMono(ArrayPtr<float> pOutput, ArrayPtr<float> pInput)
        {
            float m1 = 0;
            float m2 = _overlapLength;

            for (int i = 0; i < _overlapLength; i++)
            {
                pOutput[i] = (pInput[i] * m1 + _midBuffer[i] * m2) / _overlapLength;
                m1 += 1;
                m2 -= 1;
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

            float f1 = 0, f2 = 1.0f;

            for (int i = 0; i < 2 * _overlapLength; i += 2)
            {                
                pOutput[i + 0] = pInput[i + 0]*f1 + _midBuffer[i + 0]*f2;
                pOutput[i + 1] = pInput[i + 1]*f1 + _midBuffer[i + 1]*f2;

                f1 += fScale;
                f2 -= fScale;
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

        protected override double CalculateCrossCorr(ArrayPtr<float> mixingPos, ArrayPtr<float> compare)
        {
            double corr =0, norm = 0;
            // Same routine for stereo and mono. For Stereo, unroll by factor of 2.
            // For mono it's same routine yet unrolled by factor of 4.
            for (int i = 0; i < _channels * _overlapLength; i += 4)
            {
                corr += mixingPos[i] * compare[i] +
                        mixingPos[i + 1] * compare[i + 1];

                norm += mixingPos[i] * mixingPos[i] +
                        mixingPos[i + 1] * mixingPos[i + 1];

                // unroll the loop for better CPU efficiency:
                corr += mixingPos[i + 2] * compare[i + 2] +
                        mixingPos[i + 3] * compare[i + 3];

                norm += mixingPos[i + 2] * mixingPos[i + 2] +
                        mixingPos[i + 3] * mixingPos[i + 3];
            }

            if (norm < 1e-9) norm = 1.0;    // to avoid div by zero
            return corr / Math.Sqrt(norm);
        }
    }
}