// License :
//
// SoundTouch audio processing library
// Copyright (c) Olli Parviainen
// C# port Copyright (c) Olaf Woudenberg
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

namespace SoundTouch
{
    using System;
    using System.Diagnostics;

    using SoundTouch.Assets;
    using JetBrains.Annotations;

    internal class FirFilter
    {
        // Memory for filter coefficients
        private float[]? _filterCoeffs;
        private float[]? _filterCoeffsStereo;

        public FirFilter()
        {
            Length = 0;
            _filterCoeffs = null;
            _filterCoeffsStereo = null;
        }

        // Number of FIR filter taps
        public int Length { get; private set; }

        public virtual void SetCoefficients(in ReadOnlySpan<float> coeffs, int resultDivFactor)
        {
            if (coeffs.IsEmpty)
                throw new ArgumentException(Strings.Argument_EmptyCoefficients, nameof(coeffs));
            if ((coeffs.Length % 8) != 0)
                throw new ArgumentException(Strings.Argument_CoefficientsFilterNotDivisible, nameof(coeffs));

            Length = coeffs.Length;

            // Result divider factor in 2^k format
            var resultDivider = (float)Math.Pow(2.0, resultDivFactor);

            double scale = 1.0 / resultDivider;

            _filterCoeffs = new float[Length];
            _filterCoeffsStereo = new float[Length * 2];

            for (int i = 0; i < Length; i++)
            {
                _filterCoeffs[i] = (float)(coeffs[i] * scale);

                // create also stereo set of filter coefficients: this allows compiler
                // to autovectorize filter evaluation much more efficiently
                _filterCoeffsStereo[2 * i] = (float)(coeffs[i] * scale);
                _filterCoeffsStereo[(2 * i) + 1] = (float)(coeffs[i] * scale);
            }
        }

        /// <summary>
        /// Applies the filter to the given sequence of samples.
        /// Note : The amount of outputted samples is by value of 'filter_length'
        /// smaller than the amount of input samples.
        /// </summary>
        /// <returns>Number of samples copied to <paramref name="dest"/>.</returns>
        public int Evaluate(in Span<float> dest, in ReadOnlySpan<float> src, int numSamples, int numChannels)
        {
            if (Length <= 0)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            if (numSamples < Length)
                return 0;

#if !USE_MULTICH_ALWAYS
            if (numChannels == 1)
            {
                return EvaluateFilterMono(dest, src, numSamples);
            }

            if (numChannels == 2)
            {
                return EvaluateFilterStereo(dest, src, numSamples);
            }
#endif // USE_MULTICH_ALWAYS
            Debug.Assert(numChannels > 0, "Multiple channels");
            return EvaluateFilterMulti(dest, src, numSamples, numChannels);
        }

        [Pure]
        protected virtual int EvaluateFilterStereo(in Span<float> dest, in ReadOnlySpan<float> src, int numSamples)
        {
            if (Length <= 0 || _filterCoeffsStereo is null)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            // hint compiler autovectorization that loop length is divisible by 8
            int ilength = Length & -8;

            var end = 2 * (numSamples - ilength);

            for (int j = 0; j < end; j += 2)
            {
                double sumLeft = 0, sumRight = 0;
                ReadOnlySpan<float> ptr = src.Slice(j);

                for (int i = 0; i < ilength; i++)
                {
                    sumLeft += ptr[2 * i] * _filterCoeffsStereo[2 * i];
                    sumRight += ptr[(2 * i) + 1] * _filterCoeffsStereo[(2 * i) + 1];
                }

                dest[j] = (float)sumLeft;
                dest[j + 1] = (float)sumRight;
            }

            return numSamples - ilength;
        }

        [Pure]
        protected virtual int EvaluateFilterMono(in Span<float> dest, in ReadOnlySpan<float> src, int numSamples)
        {
            if (Length <= 0 || _filterCoeffs is null)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            // hint compiler autovectorization that loop length is divisible by 8
            int ilength = Length & -8;

            var end = numSamples - ilength;

            for (int j = 0; j < end; j++)
            {
                ReadOnlySpan<float> pSrc = src.Slice(j);

                double sum = 0;
                for (int i = 0; i < ilength; i++)
                {
                    sum += pSrc[i] * _filterCoeffs[i];
                }

                dest[j] = (float)sum;
            }

            return end;
        }

        protected virtual int EvaluateFilterMulti(in Span<float> dest, in ReadOnlySpan<float> src, int numSamples, int numChannels)
        {
            if (numChannels >= 16)
                throw new ArgumentOutOfRangeException(Strings.Argument_IllegalNumberOfChannels);
            if (Length <= 0 || _filterCoeffs is null)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            // hint compiler autovectorization that loop length is divisible by 8
            int ilength = Length & -8;

            int end = numChannels * (numSamples - ilength);

            for (int j = 0; j < end; j += numChannels)
            {
                ReadOnlySpan<float> ptr;
                Span<double> sums = stackalloc double[16];
                int c, i;

                for (c = 0; c < numChannels; c++)
                {
                    sums[c] = 0;
                }

                ptr = src.Slice(j);

                for (i = 0; i < ilength; i++)
                {
                    float coef = _filterCoeffs[i];
                    for (c = 0; c < numChannels; c++)
                    {
                        sums[c] += ptr[0] * coef;
                        ptr = ptr.Slice(1);
                    }
                }

                for (c = 0; c < numChannels; c++)
                {
                    dest[j + c] = (float)sums[c];
                }
            }

            return numSamples - ilength;
        }
    }
}
