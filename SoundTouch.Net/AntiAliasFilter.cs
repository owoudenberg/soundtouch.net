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
    /// <summary>
    /// Anti-alias filter is used to prevent folding of high frequencies when 
    /// transposing the sample rate with interpolation.
    /// </summary>
    public class AntiAliasFilter<TSampleType> where TSampleType : struct
    {
        private readonly FirFilter<TSampleType> _firFilter;
        private double _cutoffFreq;
        private int _length;

        public AntiAliasFilter(int length)
        {
            _firFilter = FirFilter<TSampleType>.NewInstance();
            _cutoffFreq = 0.5;
            SetLength(length);
        }

        /// <summary>
        /// Calculate the FIR coefficients realizing the given cutoff-frequency.
        /// </summary>
        private void CalculateCoeffs()
        {
            double temp;

            Debug.Assert(_length >= 2);
            Debug.Assert(_length % 4 == 0);
            Debug.Assert(_cutoffFreq >= 0);
            Debug.Assert(_cutoffFreq <= 0.5);

            ArrayPtr<double> work = new double[_length];
            ArrayPtr<TSampleType> coeffs = new TSampleType[_length];

            double fc2 = 2.0 * _cutoffFreq;
            double wc = Math.PI * fc2;
            double tempCoeff = (Math.PI * 2) / _length;

            double sum = 0;
            for (int i = 0; i < _length; i++)
            {
                double cntTemp = i - ((double)_length / 2);

                temp = cntTemp * wc;
                double h;
                if (temp != 0)
                {
                    h = fc2 * Math.Sin(temp) / temp; // sinc function
                }
                else
                {
                    h = 1.0;
                }
                double w = 0.54 + 0.46 * Math.Cos(tempCoeff * cntTemp);

                temp = w * h;
                work[i] = temp;

                // calc net sum of coefficients 
                sum += temp;
            }

            // ensure the sum of coefficients is larger than zero
            Debug.Assert(sum > 0);

            // ensure we've really designed a lowpass filter...
            Debug.Assert(work[_length / 2] > 0);
            Debug.Assert(work[_length / 2 + 1] > -1e-6);
            Debug.Assert(work[_length / 2 - 1] > -1e-6);

            // Calculate a scaling coefficient in such a way that the result can be
            // divided by 16384
            double scaleCoeff = 16384.0f / sum;

            for (int i = 0; i < _length; i++)
            {
                // scale & round to nearest integer
                temp = work[i] * scaleCoeff;
                temp += (temp >= 0) ? 0.5 : -0.5;
                // ensure no overfloods
                Debug.Assert(temp >= -32768 && temp <= 32767);
                coeffs[i] = (TSampleType) Convert.ChangeType(temp, typeof(TSampleType));
            }

            // Set coefficients. Use divide factor 14 => divide result by 2^14 = 16384
            _firFilter.SetCoefficients(coeffs, _length, 14);
        }

        /// <summary>
        /// Applies the filter to the given sequence of samples. 
        /// </summary>
        /// <remarks>The amount of outputted samples is by value of 'filter
        /// length' smaller than the amount of input samples.</remarks>
        public int Evaluate(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, int numSamples, int numChannels)
        {
            return _firFilter.Evaluate(dst, src, numSamples, numChannels);
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        /// <returns></returns>
        public int GetLength()
        {
            return _firFilter.GetLength();
        }

        /// <summary>
        /// Sets new anti-alias filter cut-off edge frequency, scaled to
        /// sampling  frequency (nyquist frequency = 0.5). The filter will cut
        /// off the  frequencies than that.
        /// </summary>
        public void SetCutoffFreq(double newCutoffFreq)
        {
            _cutoffFreq = newCutoffFreq;
            CalculateCoeffs();
        }

        /// <summary>
        /// Sets number of FIR filter taps, i.e. ~filter complexity
        /// </summary>
        public void SetLength(int newLength)
        {
            _length = newLength;
            CalculateCoeffs();
        }
    }
}