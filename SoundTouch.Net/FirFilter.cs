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
using System.Runtime.InteropServices;
using SoundTouch.Utility;

namespace SoundTouch
{
    internal abstract class FirFilter<TSampleType> where TSampleType : struct
    {
        private static readonly int SIZEOF_SAMPLETYPE = Marshal.SizeOf(typeof(TSampleType));

        protected TSampleType[] _filterCoeffs;
        protected int _length;
        private int _lengthDiv8;
        protected int _resultDivFactor;

        public int Evaluate(ArrayPtr<TSampleType> dest, ArrayPtr<TSampleType> src, int numSamples, int numChannels)
        {
            Debug.Assert(numChannels == 1 || numChannels == 2);

            Debug.Assert(_length > 0);
            Debug.Assert(_lengthDiv8 * 8 == _length);
            if (numSamples < _length)
            {
                return 0;
            }
            if (numChannels == 2)
            {
                return EvaluateFilterStereo(dest, src, numSamples);
            }
            return EvaluateFilterMono(dest, src, numSamples);
        }

        protected abstract int EvaluateFilterMono(ArrayPtr<TSampleType> dest, ArrayPtr<TSampleType> src, int numSamples);
        protected abstract int EvaluateFilterStereo(ArrayPtr<TSampleType> dest, ArrayPtr<TSampleType> src, int numSamples);

        public int GetLength()
        {
            return _length;
        }

        /// <exception cref="InvalidOperationException">Can't create a 
        /// <see cref="FirFilter{TSampleType}"/> instance for specified type.
        /// Only <c>short</c> and <c>float</c> are supported.</exception>
        public static FirFilter<TSampleType> NewInstance()
        {
            if (typeof(TSampleType) == typeof(short))
                return (FirFilter<TSampleType>)((object)new FirFilterInteger());
            if (typeof(TSampleType) == typeof(float))
                return (FirFilter<TSampleType>)((object)new FirFilterFloat());

            throw new InvalidOperationException(string.Format("Can't create a TimeStretch instance for type {0}. Only <short> and <float> are supported.", typeof(TSampleType)));
        }

        /// <summary>
        /// Set filter coefficients and length.
        /// </summary>
        /// <exception cref="ArgumentException">FIR filter length not divisible
        /// by 8</exception>
        public virtual void SetCoefficients(ArrayPtr<TSampleType> coeffs, int newLength, int resultDivFactor)
        {
            Debug.Assert(newLength > 0);
            if ((newLength % 8) != 0)
                throw new ArgumentException("FIR filter length not divisible by 8");

            _lengthDiv8 = newLength / 8;
            _length = _lengthDiv8 * 8;
            Debug.Assert(_length == newLength);
            _resultDivFactor = resultDivFactor;

            var numPtr = new TSampleType[_length];
            _filterCoeffs = numPtr;
            ArrayPtr<TSampleType>.CopyBytes(_filterCoeffs, coeffs, _length * SIZEOF_SAMPLETYPE);
        }
    }
}