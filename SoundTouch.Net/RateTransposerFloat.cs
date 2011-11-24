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

using SoundTouch.Utility;

using TSampleType = System.Single;
using TLongSampleType = System.Double;

namespace SoundTouch
{
    /// <summary>
    /// A linear sample rate transposer class that uses floating point arithmetics
    /// for the transposing.
    /// </summary>
    public sealed class RateTransposerFloat : RateTransposer<TSampleType>
    {
        private float _slopeCount;
        private TSampleType _previousSampleLeft, _previousSampleRight;

        public RateTransposerFloat()
        {
            ResetRegisters();
            SetRate(1.0f);
        }

        protected override void ResetRegisters()
        {
            _slopeCount = 0;
            _previousSampleLeft =
            _previousSampleRight = default(TSampleType);
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear
        /// interpolation. 'Mono' version of the routine.
        /// </summary>
        /// <returns>The number of samples returned in the 
        /// <paramref name="dst"/> buffer</returns>
        protected override int TransposeMono(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples)
        {
            int used = 0;    
            int i = 0;

            // Process the last sample saved from the previous call first...
            while (_slopeCount <= 1.0f) 
            {
                dst[i] = (1.0f - _slopeCount) * _previousSampleLeft + _slopeCount * src[0];
                i++;
                _slopeCount += Rate;
            }
            _slopeCount -= 1.0f;

            if (numSamples > 1)
            {
                while (true)
                {
                    while (_slopeCount > 1.0f) 
                    {
                        _slopeCount -= 1.0f;
                        used ++;
                        if (used >= numSamples - 1) goto end;
                    }
                    dst[i] = (1.0f - _slopeCount) * src[used] + _slopeCount * src[used + 1];
                    i++;
                    _slopeCount += Rate;
                }
            }
        end:
            // Store the last sample for the next round
            _previousSampleLeft = src[numSamples - 1];

            return i;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear
        /// interpolation.  'Mono' version of the routine.
        /// </summary>
        /// <returns>The number of samples returned in the 
        /// <paramref name="dst"/> buffer</returns>
        protected override int TransposeStereo(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples)
        {
            if (numSamples == 0) return 0;  // no samples, no work

            int used = 0;
            int i = 0;

            // Process the last sample saved from the sPrevSampleLious call first...
            while (_slopeCount <= 1.0f)
            {
                dst[2 * i] = ((1.0f - _slopeCount) * _previousSampleLeft + _slopeCount * src[0]);
                dst[2 * i + 1] = ((1.0f - _slopeCount) * _previousSampleRight + _slopeCount * src[1]);
                i++;
                _slopeCount += Rate;
            }
            // now always (iSlopeCount > 1.0f)
            _slopeCount -= 1.0f;

            if (numSamples > 1)
            {
                while (true)
                {
                    while (_slopeCount > 1.0f)
                    {
                        _slopeCount -= 1.0f;
                        used++;
                        if (used >= numSamples - 1) goto end;
                    }
                    int srcPos = 2 * used;

                    dst[2 * i] = ((1.0f - _slopeCount) * src[srcPos] + _slopeCount * src[srcPos + 2]);
                    dst[2 * i + 1] = ((1.0f - _slopeCount) * src[srcPos + 1] + _slopeCount * src[srcPos + 3]);

                    i++;
                    _slopeCount += Rate;
                }
            }
        end:
            // Store the last sample for the next round
            _previousSampleLeft = src[2 * numSamples - 2];
            _previousSampleRight = src[2 * numSamples - 1];

            return i;
        }
    }
}