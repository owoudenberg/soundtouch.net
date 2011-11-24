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

using System.Diagnostics;
using System.Runtime.InteropServices;

using SoundTouch.Utility;

namespace SoundTouch
{
    /// <summary>
    /// Sample buffer working in FIFO (first-in-first-out) principle. The class
    /// takes care of storage size adjustment and data moving during
    /// input/output operations.
    /// </summary>
    /// <remarks>
    /// Notice that in case of stereo audio, one sample is considered to consist
    /// of  both channel data.
    /// </remarks>
    public sealed class FifoSampleBuffer<TSampleType> : FifoSamplePipe<TSampleType> where TSampleType : struct
    {
        private readonly int SIZEOF_SAMPLETYPE = Marshal.SizeOf(typeof(TSampleType));
        
        /// <summary>Sample buffer.</summary>
        private TSampleType[] _buffer;

        /// <summary>
        /// Current position pointer to the buffer. This pointer is increased
        /// when samples are  removed from the pipe so that it's necessary to
        /// actually rewind buffer (move data) only new data when is put to the
        /// pipe.
        /// </summary>
        private int _bufferPos;

        /// <summary>Channels, 1=mono, 2=stereo.</summary>
        private int _channels;

        /// <summary>How many samples are currently in buffer.</summary>
        private int _samplesInBuffer;

        /// <summary>Sample buffer size in bytes.</summary>
        private int _sizeInBytes;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="numChannels">Number of channels, 1=mono, 2=stereo.
        /// Default is stereo.</param>
        public FifoSampleBuffer(int numChannels = 2)
        {
            Debug.Assert(numChannels > 0);

            _sizeInBytes = 0; // reasonable initial value
            _buffer = null;
            _samplesInBuffer = 0;
            _bufferPos = 0;
            _channels = numChannels;

            EnsureCapacity(32); // allocate initial capacity 
        }

        /// <summary>
        /// Clears all the samples.
        /// </summary>
        public override void Clear()
        {
            _samplesInBuffer = 0;
            _bufferPos = 0;
        }

        /// <summary>
        /// Ensures that the buffer has capacity for at least this many samples.
        /// </summary>
        private void EnsureCapacity(int capacityRequirement)
        {
            if (capacityRequirement > GetCapacity())
            {
                // enlarge the buffer in 4kbyte steps (round up to next 4k boundary)
                _sizeInBytes = (int) ((capacityRequirement * _channels * SIZEOF_SAMPLETYPE + 4095) & unchecked((uint)-4096));
                Debug.Assert(_sizeInBytes % 2 == 0);
                var temp = new TSampleType[_sizeInBytes / SIZEOF_SAMPLETYPE + 16 / SIZEOF_SAMPLETYPE];

                if (_samplesInBuffer != 0)
                    ArrayPtr<TSampleType>.CopyBytes(temp, PtrBegin(), _samplesInBuffer * _channels * SIZEOF_SAMPLETYPE);

                _buffer = temp;
                _bufferPos = 0;
            }
            else
            {
                // simply rewind the buffer (if necessary)
                Rewind();
            }
        }

        /// <summary>
        /// Returns current capacity.
        /// </summary>
        private int GetCapacity()
        {
            return _sizeInBytes / (_channels * SIZEOF_SAMPLETYPE);
        }

        /// <summary>
        /// Gets a value indicating whether there aren't any samples available
        /// for outputting.
        /// </summary>
        /// <value>
        ///   <c>true</c> if there aren't any samples available for outputting;
        ///   otherwise, <c>false</c>.
        /// </value>
        public override bool IsEmpty
        {
            get { return _samplesInBuffer == 0; }
        }

        /// <summary>
        /// Returns number of samples currently available.
        /// </summary>
        public override int AvailableSamples
        {
            get { return _samplesInBuffer; }
        }

        /// <summary>
        /// Returns a pointer to the beginning of the output samples.  This
        /// function is provided for accessing the output samples directly. 
        /// Please be careful for not to corrupt the book-keeping!
        ///
        /// When using this function to output samples, also remember to
        /// 'remove' the output samples from the buffer by calling the 
        /// <see cref="ReceiveSamples(int)"/> function.
        /// </summary>
        public override ArrayPtr<TSampleType> PtrBegin()
        {
            Debug.Assert(_buffer != null);
            return (ArrayPtr<TSampleType>)_buffer + _bufferPos * _channels;
        }

        /// <summary>
        /// Returns a pointer to the end of the used part of the sample buffer
        /// (i.e.  where the new samples are to be inserted). This function may
        /// be used for  inserting new samples into the sample buffer directly.
        /// Please be careful not corrupt the book-keeping!
        ///
        /// When using this function as means for inserting new samples, also
        /// remember  to increase the sample count afterwards, by calling  the 
        /// <see cref="PutSamples(int)"/> function.
        /// </summary>
        /// <param name="slackCapacity">How much free capacity (in samples)
        /// there _at least_  should be so that the caller can successfully
        /// insert the  desired samples to the buffer. If necessary, the
        /// function  grows the buffer size to comply with this requirement.
        /// </param>
        /// <returns>Pointer to the end of the used part of the sample buffer
        /// </returns>
        public ArrayPtr<TSampleType> PtrEnd(int slackCapacity)
        {
            EnsureCapacity(_samplesInBuffer + slackCapacity);
            return (ArrayPtr<TSampleType>)_buffer + _samplesInBuffer * _channels;
        }

        /// <summary>
        /// Adjusts the book-keeping to increase number of samples in the buffer
        /// without  copying any actual samples.
        ///
        /// This function is used to update the number of samples in the sample
        /// buffer when accessing the buffer directly with <see cref="PtrEnd"/>
        /// function. Please be  careful though!
        /// </summary>
        /// <param name="numSamples">Number of samples been inserted.</param>
        public void PutSamples(int numSamples)
        {
            int req = _samplesInBuffer + numSamples;
            EnsureCapacity(req);
            _samplesInBuffer += numSamples;
        }

        /// <summary>
        /// Adds <paramref name="numSamples"/> pcs of samples from the 
        /// <paramref name="samples"/> memory position to the sample buffer.
        /// </summary>
        /// <param name="samples">Pointer to samples.</param>
        /// <param name="numSamples">Number of samples to insert.</param>
        public override void PutSamples(ArrayPtr<TSampleType> samples, int numSamples)
        {
            ArrayPtr<TSampleType>.CopyBytes(PtrEnd(numSamples), samples, SIZEOF_SAMPLETYPE * numSamples * _channels);
            _samplesInBuffer += numSamples;
        }

        /// <summary>
        /// Adjusts book-keeping so that given number of samples are removed
        /// from beginning of the  sample buffer without copying them anywhere. 
        ///
        /// Used to reduce the number of samples in the buffer when accessing
        /// the sample buffer directly with <see cref="PtrBegin"/> function.
        /// </summary>
        ///<param name="maxSamples">Remove this many samples from the beginning
        ///of pipe.</param>
        public override int ReceiveSamples(int maxSamples)
        {
            if (maxSamples >= _samplesInBuffer)
            {
                int temp = _samplesInBuffer;
                _samplesInBuffer = 0;
                return temp;
            }

            _samplesInBuffer -= maxSamples;
            _bufferPos += maxSamples;

            return maxSamples;
        }

        /// <summary>
        /// Output samples from beginning of the sample buffer. Copies requested
        /// samples to  output buffer and removes them from the sample buffer.
        /// If there are less than 
        /// <paramref name="maxSamples"/> samples in the buffer, returns all
        /// that available.
        /// </summary>
        /// <param name="output">Buffer where to copy output samples.</param>
        /// <param name="maxSamples">How many samples to receive at max.</param>
        /// <returns>Number of samples returned.</returns>
        public override int ReceiveSamples(ArrayPtr<TSampleType> output, int maxSamples)
        {
            int num = (maxSamples > _samplesInBuffer) ? _samplesInBuffer : maxSamples;

            ArrayPtr<TSampleType>.CopyBytes(output, PtrBegin(), _channels * SIZEOF_SAMPLETYPE * num);
            return ReceiveSamples(num);
        }

        /// <summary>
        /// Rewind the buffer by moving data from position pointed by 
        /// <see cref="_bufferPos"/> to real  beginning of the buffer.
        /// </summary>
        /// <remarks>
        /// if output location pointer <see cref="_bufferPos"/> isn't zero,
        /// 'rewinds' the buffer and zeroes this pointer by copying samples from
        /// the <see cref="_bufferPos"/> pointer  location on to the beginning
        /// of the buffer.
        /// </remarks>
        private void Rewind()
        {
            if ((_buffer == null) || (_bufferPos == 0)) return;
            
            ArrayPtr<TSampleType>.CopyBytes(_buffer, PtrBegin(), SIZEOF_SAMPLETYPE * _channels * _samplesInBuffer);
            _bufferPos = 0;
        }

        /// <summary>
        /// Sets number of channels, 1 = mono, 2 = stereo.
        /// </summary>
        public void SetChannels(int numChannels)
        {
            Debug.Assert(numChannels > 0);

            int usedBytes = _channels * _samplesInBuffer;
            _channels = numChannels;
            _samplesInBuffer = usedBytes / _channels;
        }
    }
}