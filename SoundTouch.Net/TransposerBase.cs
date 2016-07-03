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
    /// Abstract base class for transposer implementations (linear, advanced vs integer, float etc)
    /// </summary>
    public abstract class TransposerBase<TSampleType> where TSampleType : struct
    {
        public enum Algoritm
        {
            Linear = 0,
            Cubic,
            Shannon
        }

        protected static Algoritm _algoritm = Algoritm.Cubic;

        protected abstract void ResetRegisters();

        protected abstract int TransposeMono(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, ref int srcSamples);

        protected abstract int TransposeStereo(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, ref int srcSamples);

        protected abstract int TransposeMulti(ArrayPtr<TSampleType> dst, ArrayPtr<TSampleType> src, ref int srcSamples);

        public double rate;
        public int channels;

        public TransposerBase()
        {
            channels = 0;
            rate = 1.0f;
        }

        public virtual int Transpose(FifoSampleBuffer<TSampleType> dest, FifoSampleBuffer<TSampleType> src)
        {
            int numSrcSamples = src.AvailableSamples;
            int sizeDemand = (int) (numSrcSamples/rate) + 8;
            int numOutput;

            ArrayPtr<TSampleType> psrc = src.PtrBegin();
            ArrayPtr<TSampleType> pdest = dest.PtrEnd(sizeDemand);

#if !USE_MULTICH_ALWAYS
            if (channels == 1)
            {
                numOutput = TransposeMono(pdest, psrc, ref numSrcSamples);
            }
            else if (channels == 2)
            {
                numOutput = TransposeStereo(pdest, psrc, ref numSrcSamples);
            }
            else
#endif
            {
                Debug.Assert(channels > 0);
                numOutput = TransposeMulti(pdest, psrc, ref numSrcSamples);
            }
            dest.PutSamples(numOutput);
            src.ReceiveSamples(numSrcSamples);
            return numOutput;
        }

        public virtual void SetRate(double newRate)
        {
            this.rate = newRate;
        }

        public virtual void SetChannels(int channels)
        {
            this.channels = channels;
        }
        
        public static void SetAlgoritm(Algoritm a)
        {
            _algoritm = a;
        }
    }
}