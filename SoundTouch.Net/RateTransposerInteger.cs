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

using TSampleType = System.Int16;
using TLongSampleType = System.Int64;

namespace SoundTouch
{
    /// <summary>
    /// A linear sample rate transposer class that uses integer arithmetics.
    /// for the transposing.
    /// </summary>
    internal sealed class RateTransposerInteger : RateTransposer<TSampleType> 
    {
        private const int SCALE = 65536;

        private int _slopeCount;
        private int _rate;

        private TSampleType _previousSampleLeft, _previousSampleRight;

        public RateTransposerInteger()
        {
            ResetRegisters();
            SetRate(1.0f);
        }

        protected override void ResetRegisters()
        {
            _slopeCount = 0;
            _previousSampleRight = default(TSampleType);
            _previousSampleLeft = default(TSampleType);
        }

        /// <summary>
        /// Sets new target rate. Normal rate = 1.0, smaller values represent
        /// slower  rate, larger faster rates.
        /// </summary>
        public override void SetRate(float newRate)
        {
            _rate = (int)((newRate * 65536.0) + 0.5);
            base.SetRate(newRate);
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear
        /// interpolation. 'Mono' version of the routine.
        /// </summary>
        /// <returns>The number of samples returned in the 
        /// <paramref name="dst"/> buffer</returns>
        protected override int TransposeMono(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples)
        {
            TLongSampleType temp, vol1;

            if (numSamples == 0) return 0;  // no samples, no work

	        int used = 0;    
            int i = 0;

            // Process the last sample saved from the previous call first...
            while (_slopeCount <= SCALE) 
            {
                vol1 = SCALE - _slopeCount;
                temp = vol1 * _previousSampleLeft + _slopeCount * src[0];
                dst[i] = (TSampleType)(temp / SCALE);
                i++;
                _slopeCount += _rate;
            }
            // now always (iSlopeCount > SCALE)
            _slopeCount -= SCALE;

            while (true)
            {
                while (_slopeCount > SCALE) 
                {
                    _slopeCount -= SCALE;
                    used ++;
                    if (used >= numSamples - 1) goto end;
                }
                vol1 = SCALE - _slopeCount;
                temp = src[used] * vol1 + _slopeCount * src[used + 1];
                dst[i] = (TSampleType)(temp / SCALE);

                i++;
                _slopeCount += _rate;
            }
        end:
            // Store the last sample for the next round
            _previousSampleLeft = src[numSamples - 1];

            return i;
        }

        /// <summary>
        /// Transposes the sample rate of the given samples using linear
        /// interpolation.  // 'Stereo' version of the routine. 
        /// </summary>
        /// <returns>The number of samples returned in the 
        /// <paramref name="dst"/> buffer</returns>
        protected override int TransposeStereo(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples)
        {
            TLongSampleType temp, vol1;

            if (numSamples == 0) return 0;  // no samples, no work

            int used = 0;    
            int i = 0;

            // Process the last sample saved from the sPrevSampleLious call first...
            while (_slopeCount <= SCALE) 
            {
                vol1 = SCALE - _slopeCount;
                temp = vol1 * _previousSampleLeft + _slopeCount * src[0];
                dst[2 * i] = (TSampleType)(temp / SCALE);
                temp = vol1 * _previousSampleRight + _slopeCount * src[1];
                dst[2 * i + 1] = (TSampleType)(temp / SCALE);
                i++;
                _slopeCount += _rate;
            }
            // now always (iSlopeCount > SCALE)
            _slopeCount -= SCALE;

            while (true)
            {
                while (_slopeCount > SCALE) 
                {
                    _slopeCount -= SCALE;
                    used ++;
                    if (used >= numSamples - 1) goto end;
                }
                int srcPos = 2 * used;
                vol1 = SCALE - _slopeCount;
                temp = src[srcPos] * vol1 + _slopeCount * src[srcPos + 2];
                dst[2 * i] = (TSampleType)(temp / SCALE);
                temp = src[srcPos + 1] * vol1 + _slopeCount * src[srcPos + 3];
                dst[2 * i + 1] = (TSampleType)(temp / SCALE);

                i++;
                _slopeCount += _rate;
            }
        end:
            // Store the last sample for the next round
            _previousSampleLeft = src[2 * numSamples - 2];
            _previousSampleRight = src[2 * numSamples - 1];

            return i;
        }
    }
}