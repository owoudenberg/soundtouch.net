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

namespace SoundStretch
{
    /// <summary>
    /// Helper functions for swapping byte order to correctly read/write WAV
    /// files  with big-endian CPU's.
    ///
    /// For example, Intel x86 is little-endian and doesn't require conversion,
    /// while PowerPC of Mac's and many other RISC cpu's are big-endian.
    /// </summary>
    internal abstract class EndianHelper
    {
        /// <summary>helper-function to swap byte-order of 32bit integer</summary>
        public abstract void Swap32(ref int dwData);

        /// <summary>helper-function to swap byte-order of 16bit integer</summary>
        public abstract void Swap16(ref short wData);

        /// <summary>helper-function to swap byte-order of buffer of 16bit integers</summary>
        public abstract void Swap16Buffer(short[] pData, int dwNumWords);

        public static EndianHelper NewInstance()
        {
            if (BitConverter.IsLittleEndian)
                return new LittleEndianHelper();
            return new BigEndianHelper();
        }
    }

    internal sealed class LittleEndianHelper : EndianHelper
    {
        public override void Swap32(ref int dwData)
        {
        }

        public override void Swap16(ref short wData)
        {
        }

        public override void Swap16Buffer(short[] pData, int dwNumWords)
        {
        }
    }

    internal sealed class BigEndianHelper : EndianHelper
    {

        public override void Swap32(ref int dwData)
        {
            var data = unchecked((uint)dwData);
            dwData = unchecked((int) (((data >> 24) & 0x000000FF) |
                                      ((data >> 8) & 0x0000FF00) |
                                      ((data << 8) & 0x00FF0000) |
                                      ((data << 24) & 0xFF000000)));
        }

        public override void Swap16(ref short wData)
        {
            wData = unchecked((short)(((wData >> 8) & 0x00FF) |
                                       ((wData << 8) & 0xFF00)));
        }

        public override void Swap16Buffer(short[] pData, int dwNumWords)
        {
            for (int i = 0; i < dwNumWords; i++)
            {
                Swap16(ref pData[i]);
            }
        }
    }
}