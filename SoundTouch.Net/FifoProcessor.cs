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

using SoundTouch.Utility;

namespace SoundTouch
{
    /// <summary>
    /// Base-class for sound processing routines working in FIFO principle. With
    /// this base  class it's easy to implement sound processing stages that can
    /// be chained together, so that samples that are fed into beginning of the
    /// pipe automatically go through  all the processing stages.
    ///
    /// When samples are input to this class, they're first processed and then
    /// put to  the FIFO pipe that's defined as output of this class. This
    /// output pipe can be either other processing stage or a FIFO sample
    /// buffer.
    /// </summary>
    public abstract class FifoProcessor<TSampletype> : FifoSamplePipe<TSampletype> where TSampletype : struct
    {
        /// Internal pipe where processed samples are put.
        protected FifoSamplePipe<TSampletype> Output;

        /// <summary>
        /// Constructor. Doesn't define output pipe; it has to be set be 
        /// <see cref="SetOutPipe"/> function.
        /// </summary>
        protected FifoProcessor()
        {
            Output = null;
        }

        /// <summary>
        /// Constructor. Configures output pipe.
        /// </summary>
        /// <param name="pOutput">Output pipe.</param>
        protected FifoProcessor(FifoSamplePipe<TSampletype> pOutput)
        {
            Debug.Assert(pOutput != null);
            Output = pOutput;
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
            get { return Output.IsEmpty; }
        }

        /// <summary>
        /// Returns number of samples currently available.
        /// </summary>
        /// <returns>Number of samples currently available</returns>
        public override int AvailableSamples
        {
            get { return Output.AvailableSamples; }
        }

        /// <summary>
        /// This function is provided for accessing the output samples directly.
        /// Please be careful for not to corrupt the book-keeping!
        ///
        /// When using this function to output samples, also remember to
        /// 'remove' the output samples from the buffer by calling the 
        /// <see cref="ReceiveSamples(int)"/>function
        /// </summary>
        /// <returns>Pointer to the beginning of the output samples.</returns>
        public override ArrayPtr<TSampletype> PtrBegin()
        {
            return Output.PtrBegin();
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
            return Output.ReceiveSamples(maxSamples);
        }

        /// <summary>
        /// Output samples from beginning of the sample buffer. Copies requested
        /// samples to  output buffer and removes them from the sample buffer.
        /// If there are less than 
        /// <paramref name="maxSamples"/> samples in the buffer, returns all
        /// that available.
        /// </summary>
        /// <param name="outBuffer">Buffer where to copy output samples.</param>
        /// <param name="maxSamples">How many samples to receive at max.</param>
        /// <returns>Number of samples returned.</returns>
        public override int ReceiveSamples(ArrayPtr<TSampletype> outBuffer, int maxSamples)
        {
            return Output.ReceiveSamples(outBuffer, maxSamples);
        }

        /// <summary>
        /// Sets output pipe.
        /// </summary>
        protected void SetOutPipe(FifoSamplePipe<TSampletype> pOutput)
        {
            Debug.Assert(Output == null);
            Debug.Assert(pOutput != null);
            Output = pOutput;
        }
    }
}