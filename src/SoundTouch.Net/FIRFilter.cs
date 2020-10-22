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
        // Result divider value.
        private float _resultDivider;

        // Memory for filter coefficients
        private float[]? _filterCoeffs;

        public FirFilter()
        {
            _resultDivider = 0;
            Length = 0;
            _filterCoeffs = null;
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
            _resultDivider = (float)Math.Pow(2.0, resultDivFactor);

            _filterCoeffs = new float[Length];
            coeffs.CopyTo(_filterCoeffs);
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
            if (Length <= 0 || _filterCoeffs is null)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            // when using floating point samples, use a scaler instead of a divider
            // because division is much slower operation than multiplying.
            double dScaler = 1.0 / _resultDivider;

            var end = 2 * (numSamples - Length);

            for (int j = 0; j < end; j += 2)
            {
                double sumLeft = 0, sumRight = 0;
                ReadOnlySpan<float> ptr = src.Slice(j);

                for (int i = 0; i < Length; i++)
                {
                    sumLeft += ptr[2 * i] * _filterCoeffs[i];
                    sumRight += ptr[(2 * i) + 1] * _filterCoeffs[i];
                }

                sumRight *= dScaler;
                sumLeft *= dScaler;

                dest[j] = (float)sumLeft;
                dest[j + 1] = (float)sumRight;
            }

            return numSamples - Length;
        }

        [Pure]
        protected virtual int EvaluateFilterMono(in Span<float> dest, in ReadOnlySpan<float> src, int numSamples)
        {
            if (Length <= 0 || _filterCoeffs is null)
                throw new InvalidOperationException(Strings.InvalidOperation_CoefficientsNotInitialized);

            // when using floating point samples, use a scaler instead of a divider
            // because division is much slower operation than multiplying.
            double dScaler = 1.0 / _resultDivider;

            var end = numSamples - Length;

            for (int j = 0; j < end; j++)
            {
                ReadOnlySpan<float> pSrc = src.Slice(j);

                double sum = 0;
                for (int i = 0; i < Length; i++)
                {
                    sum += pSrc[i] * _filterCoeffs[i];
                }

                sum *= dScaler;
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

            // when using floating point samples, use a scaler instead of a divider
            // because division is much slower operation than multiplying.
            double dScaler = 1.0 / _resultDivider;

            int end = numChannels * (numSamples - Length);

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

                for (i = 0; i < Length; i++)
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
                    sums[c] *= dScaler;
                    dest[j + c] = (float)sums[c];
                }
            }

            return numSamples - Length;
        }
    }
}
