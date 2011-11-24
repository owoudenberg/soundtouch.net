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

namespace SoundTouch
{
    /// <summary>
    /// Abstract base class for FIFO (first-in-first-out) sample processing
    /// classes.
    /// </summary>
    public abstract class FifoSamplePipe<TSampletype> where TSampletype : struct
    {
        /// <summary>
        /// Clears all the samples.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Gets a value indicating whether there aren't any samples available
        /// for outputting.
        /// </summary>
        /// <value>
        ///   <c>true</c> if there aren't any samples available for outputting;
        ///   otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsEmpty { get; }

        /// <summary>
        /// Moves samples from the <paramref name="other"/> pipe instance to
        /// this instance.
        /// </summary>
        /// <param name="other">Other pipe instance where from the receive the
        /// data.</param>
        public void MoveSamples(FifoSamplePipe<TSampletype> other)
        {
            var oNumSamples = other.AvailableSamples;
            PutSamples(other.PtrBegin(), oNumSamples);
            other.ReceiveSamples(oNumSamples);
        }

        /// <summary>
        /// Returns number of samples currently available.
        /// </summary>
        /// <returns>Number of samples currently available.</returns>
        public abstract int AvailableSamples { get; }

        /// <summary>
        /// This function is provided for accessing the output samples directly.
        /// Please be careful for not to corrupt the book-keeping!
        ///
        /// When using this function to output samples, also remember to
        /// 'remove' the output samples from the buffer by calling the 
        /// <see cref="ReceiveSamples(int)"/> function
        /// </summary>
        /// <returns>Pointer to the beginning of the output samples</returns>
        public abstract ArrayPtr<TSampletype> PtrBegin();

        /// <summary>
        /// Adds <paramref name="numSamples"/> pcs of samples from the 
        /// <paramref name="samples"/> memory position to the sample buffer.
        /// </summary>
        /// <param name="samples">Pointer to samples.</param>
        /// <param name="numSamples">Number of samples to insert.</param>
        public abstract void PutSamples(ArrayPtr<TSampletype> samples, int numSamples);

        /// <summary>
        /// Adjusts book-keeping so that given number of samples are removed
        /// from beginning of the  sample buffer without copying them anywhere. 
        ///
        /// Used to reduce the number of samples in the buffer when accessing
        /// the sample buffer directly with <see cref="PtrBegin"/> function.
        /// </summary>
        ///<param name="maxSamples">Remove this many samples from the beginning
        ///of pipe.</param>
        ///<returns>Number of samples removed.</returns>
        public abstract int ReceiveSamples(int maxSamples);

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
        public abstract int ReceiveSamples(ArrayPtr<TSampletype> output, int maxSamples);
    }
}