using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SoundTouch.Utility
{
    /// <summary>
    /// A helper class working like a C++ pointer to 
    /// an array.
    /// </summary>
    /// <typeparam name="T">Array Type</typeparam>
    public class ArrayPtr<T> : IEnumerable<T>
    {
        private static readonly int SIZEOF_SAMPLETYPE = Marshal.SizeOf(typeof(T));

        private readonly T[] _buffer;
        private int _index;

        private ArrayPtr(T[] buffer, int index)
        {
            _buffer = buffer;
            _index = index;
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        public T this[int index]
        {
            get { return _buffer[_index + index]; }
            set { _buffer[_index + index] = value; }
        }

        /// <summary>
        /// Advances the pointer to the next element.
        /// </summary>
        public static ArrayPtr<T> operator ++(ArrayPtr<T> ptr)
        {
            ptr._index++;
            return ptr;
        }

        /// <summary>
        /// Returns a new Array, pointing the the element, 
        /// <paramref name="index"/> positions after the start of 
        /// <paramref name="ptrArray"/>.
        /// </summary>
        public static ArrayPtr<T> operator +(ArrayPtr<T> ptrArray, int index)
        {
            return new ArrayPtr<T>(ptrArray._buffer, ptrArray._index + index);
        }

        /// <summary>
        /// Performs an implicit conversion from array of T to <see cref="SoundTouch.Utility.ArrayPtr&lt;T&gt;"/>.
        /// </summary>
        public static implicit operator ArrayPtr<T>(T[] buffer)
        {
            return new ArrayPtr<T>(buffer, 0);
        }

        /// <summary>
        /// Returns the first element <paramref name="buffer"/> is pointing to.
        /// </summary>
        public static explicit operator T(ArrayPtr<T> buffer)
        {
            return buffer[0];
        }

        /// <summary>
        /// Returns a copy of the Array, beginning at the offset set by <see cref="ArrayPtr{T}"/>.
        /// </summary>
        public static explicit operator T[](ArrayPtr<T> buffer)
        {
            var copy = new T[buffer._buffer.Length - buffer._index];
            Array.Copy(buffer._buffer, buffer._index, copy, 0, copy.Length);
            return copy;
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return _buffer.Skip(_index).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Copies the specified amount of bytes from source buffer to destination.
        /// </summary>
        /// <param name="to">Destination.</param>
        /// <param name="from">Source.</param>
        /// <param name="byteCount">The amount of bytes to copy.</param>
        public static void CopyBytes(ArrayPtr<T> to, ArrayPtr<T> from, int byteCount)
        {
            Buffer.BlockCopy(from._buffer, from._index * SIZEOF_SAMPLETYPE, to._buffer, to._index * Marshal.SizeOf(typeof(T)), byteCount);
        }

        /// <summary>
        /// Fills the buffer with the specified value.
        /// </summary>
        public static void Fill(ArrayPtr<T> buffer, T value, int count)
        {
            int last = buffer._index + count;
            for (int index = buffer._index; index < last; ++index)
                buffer._buffer[index] = value;
        }
    }
}