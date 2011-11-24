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

namespace SoundTouch
{
    /// <summary>
    /// The routine detects highest value on an array of values and calculates
    /// the  precise peak location as a mass-center of the 'hump' around the
    /// peak value.
    /// </summary>
    public class PeakFinder
    {
        /// Min, max allowed peak positions within the data vector
        private int _maxPos;

        /// Min, max allowed peak positions within the data vector
        private int _minPos;

        /// Constructor. 
        public PeakFinder()
        {
            _minPos = _maxPos = 0;
        }

        /// <summary>
        /// Calculates the mass center between given vector items.
        /// </summary>
        /// <param name="data">Data vector.</param>
        /// <param name="firstPos">Index of first vector item belonging to the
        /// peak.</param>
        /// <param name="lastPos">Index of last vector item belonging to the
        /// peak.</param>
        private static double CalcMassCenter(float[] data, int firstPos, int lastPos)
        {
            float sum = 0;
            float wsum = 0;
            for (int i = firstPos; i <= lastPos; i++)
            {
                sum += i*data[i];
                wsum += data[i];
            }

            if (wsum < 1e-6) return 0;
            return sum/wsum;
        }

        /// <summary>
        /// Finds the data vector index where the monotonously decreasing signal
        /// crosses the given level.
        /// </summary>
        /// <param name="data">Data vector.</param>
        /// <param name="level">Goal crossing level.</param>
        /// <param name="peakpos">Peak position index within the data vector.
        /// </param>
        /// <param name="direction">Direction where to proceed from the peak: 1
        /// = right, -1 = left.</param>
        private int FindCrossingLevel(float[] data, float level, int peakpos, int direction)
        {
            float peaklevel = data[peakpos];
            Debug.Assert(peaklevel >= level);
            int pos = peakpos;
            while ((pos >= _minPos) && (pos < _maxPos))
            {
                if (data[pos + direction] < level) return pos; // crossing found
                pos += direction;
            }
            return -1; // not found
        }

        /// <summary>
        /// Finds the 'ground' level, i.e. smallest level between two
        /// neighboring peaks, to right- or left-hand side of the given peak
        /// position.
        /// </summary>
        /// <param name="data">Data vector.</param>
        /// <param name="peakpos">Peak position index within the data vector.
        /// </param>
        /// <param name="direction">Direction where to proceed from the peak: 1
        /// = right, -1 = left.</param>
        private int FindGround(float[] data, int peakpos, int direction)
        {
            int climbCount = 0;
            float refvalue = data[peakpos];
            int lowpos = peakpos;

            int pos = peakpos;

            while ((pos > _minPos) && (pos < _maxPos))
            {
                int prevpos = pos;
                pos += direction;

                // calculate derivate
                float delta = data[pos] - data[prevpos];
                if (delta <= 0)
                {
                    // going downhill, ok
                    if (climbCount != 0)
                    {
                        climbCount--; // decrease climb count
                    }

                    // check if new minimum found
                    if (data[pos] < refvalue)
                    {
                        // new minimum found
                        lowpos = pos;
                        refvalue = data[pos];
                    }
                }
                else
                {
                    // going uphill, increase climbing counter
                    climbCount++;
                    if (climbCount > 5) break; // we've been climbing too long => it's next uphill => quit
                }
            }
            return lowpos;
        }

        /// <summary>
        /// Get exact center of peak near given position by calculating local
        /// mass of center.
        /// </summary>
        private double GetPeakCenter(float[] data, int peakpos)
        {
            // find ground positions.
            int gp1 = FindGround(data, peakpos, -1);
            int gp2 = FindGround(data, peakpos, 1);

            float groundLevel = Math.Max(data[gp1], data[gp2]);
            float peakLevel = data[peakpos];

            if (groundLevel < 1e-9) return 0; // ground level too small => detection failed
            if ((peakLevel/groundLevel) < 1.3) return 0; // peak less than 30% of the ground level => no good peak detected

            // calculate 70%-level of the peak
            float cutLevel = 0.70f*peakLevel + 0.30f*groundLevel;
            // find mid-level crossings
            int crosspos1 = FindCrossingLevel(data, cutLevel, peakpos, -1);
            int crosspos2 = FindCrossingLevel(data, cutLevel, peakpos, 1);

            if ((crosspos1 < 0) || (crosspos2 < 0)) return 0; // no crossing, no peak..

            // calculate mass center of the peak surroundings
            return CalcMassCenter(data, crosspos1, crosspos2);
        }


        /// <summary>
        /// Detect exact peak position of the data vector by finding the largest
        /// peak 'hump' and calculating the mass-center location of the peak
        /// hump.
        /// </summary>
        /// <param name="data">Data vector to be analyzed. The data vector has
        /// to be at least <paramref name="maxPos"/> items long.</param>
        /// <param name="minPos">Min allowed peak location within the vector
        /// data.</param>
        /// <param name="maxPos">Max allowed peak location within the vector
        /// data.</param>
        /// <returns>The location of the largest base harmonic peak hump.
        /// </returns>
        public double DetectPeak(float[] data, int minPos, int maxPos)
        {
            int i;

            _minPos = minPos;
            _maxPos = maxPos;

            // find absolute peak
            int peakpos = minPos;
            double peak = data[minPos];
            for (i = minPos + 1; i < maxPos; i++)
            {
                if (data[i] > peak)
                {
                    peak = data[i];
                    peakpos = i;
                }
            }

            // Calculate exact location of the highest peak mass center
            double highPeak = GetPeakCenter(data, peakpos);
            peak = highPeak;

            // Now check if the highest peak were in fact harmonic of the true base beat peak 
            // - sometimes the highest peak can be Nth harmonic of the true base peak yet 
            // just a slightly higher than the true base
            for (i = 2; i < 10; i++)
            {
                peakpos = (int) (highPeak/i + 0.5f);
                if (peakpos < minPos) break;

                // calculate mass-center of possible base peak
                double peaktmp = GetPeakCenter(data, peakpos);

                // now compare to highest detected peak
                var i1 = (int) (highPeak + 0.5);
                var i2 = (int) (peaktmp + 0.5);
                double tmp = 2*(data[i2] - data[i1])/(data[i2] + data[i1]);
                if (Math.Abs(tmp) < 0.1)
                {
                    // The highest peak is harmonic of almost as high base peak,
                    // thus use the base peak instead
                    peak = peaktmp;
                }
            }

            return peak;
        }
    }
}