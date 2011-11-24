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
using System.IO;
using System.Runtime.InteropServices;

namespace SoundStretch
{
    /// <summary>Class for writing WAV audio files.</summary>
    public class WavOutFile : IDisposable
    {
        private static readonly EndianHelper _endian = EndianHelper.NewInstance();

        /// Counter of how many bytes have been written to the file so far.
        private int _bytesWritten;

        /// Pointer to the WAV file
        private Stream _fileStream;

        /// WAV file header data.
        private WavHeader _header;

        /// <summary>
        /// Creates a new WAV file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <param name="sampleRate">Sample rate (e.g. 44100 etc).</param>
        /// <param name="bits">Bits per sample (8 or 16 bits).</param>
        /// <param name="channels">Number of channels (1=mono, 2=stereo).</param>
        /// <exception cref="ArgumentException">Unable to open file for writing.</exception>
        public WavOutFile(string filename, int sampleRate, int bits, int channels)
        {
            _bytesWritten = 0;
            try
            {
                _fileStream = File.Open(filename, FileMode.Create, FileAccess.Write);
            }
            catch (Exception exception)
            {
                // didn't succeed
                string msg = string.Format("Error : Unable to open file \"{0}\" for writing.", filename);
                throw new ArgumentException(msg, exception);
            }

            FillInHeader(sampleRate, bits, channels);
            WriteHeader();
        }

        /// <exception cref="InvalidOperationException">Error : Unable to access output file stream.</exception>
        public WavOutFile(Stream file, int sampleRate, int bits, int channels)
        {
            _bytesWritten = 0;
            _fileStream = file;
            if (_fileStream == null)
            {
                const string msg = "Error : Unable to access output file stream.";
                throw new InvalidOperationException(msg);
            }

            FillInHeader(sampleRate, bits, channels);
            WriteHeader();
        }

        /// <summary>Finalizes & closes the WAV file.</summary>
        public void Dispose()
        {
            FinishHeader();
            if (_fileStream != null)
                _fileStream.Dispose();
            _fileStream = null;
        }

        /// <summary>Fills in WAV file header information.</summary>
        private void FillInHeader(int sampleRate, int bits, int channels)
        {
            // fill in the 'riff' part..

            // copy string 'RIFF' to riff_char
            _header.Riff.Riff = WavRiff.RIFF_STR.ToCharArray();
            // PackageLength unknown so far
            _header.Riff.PackageLength = 0;
            // copy string 'WAVE' to wave
            _header.Riff.Wave = WavRiff.WAVE_STR.ToCharArray();

            // fill in the 'format' part..

            // copy string 'fmt ' to fmt
            _header.Format.Fmt = WavFormat.FMT_STR.ToCharArray();

            _header.Format.FormatLen = 0x10;
            _header.Format.Fixed = 1;
            _header.Format.ChannelNumber = (short) channels;
            _header.Format.SampleRate = sampleRate;
            _header.Format.BitsPerSample = (short) bits;
            _header.Format.BytePerSample = (short) (bits*channels/8);
            _header.Format.ByteRate = _header.Format.BytePerSample*sampleRate;
            _header.Format.SampleRate = sampleRate;

            // fill in the 'data' part..

            // copy string 'data' to data_field
            _header.Data.DataField = WavData.DATA_STR.ToCharArray();
            // data_len unknown so far
            _header.Data.DataLen = 0;
        }

        /// <summary>Finishes the WAV file header by supplementing information of amount of
        /// data written to file etc</summary>
        private void FinishHeader()
        {
            // supplement the file length into the header structure
            _header.Riff.PackageLength = _bytesWritten + 36;
            _header.Data.DataLen = _bytesWritten;

            WriteHeader();
        }

        /// <summary>Writes the WAV file header.</summary>
        private void WriteHeader()
        {
            // swap byte order if necessary
            WavHeader hdrTemp = _header;
            _endian.Swap32(ref hdrTemp.Riff.PackageLength);
            _endian.Swap32(ref hdrTemp.Format.FormatLen);
            _endian.Swap16(ref hdrTemp.Format.Fixed);
            _endian.Swap16(ref hdrTemp.Format.ChannelNumber);
            _endian.Swap32(ref hdrTemp.Format.SampleRate);
            _endian.Swap32(ref hdrTemp.Format.ByteRate);
            _endian.Swap16(ref hdrTemp.Format.BytePerSample);
            _endian.Swap16(ref hdrTemp.Format.BitsPerSample);
            _endian.Swap32(ref hdrTemp.Data.DataLen);

            // write the supplemented header in the beginning of the file
            _fileStream.Seek(0, SeekOrigin.Begin);

            int size = Marshal.SizeOf(hdrTemp);
            var data = new byte[size];

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(hdrTemp, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            _fileStream.Write(data, 0, size);

            // jump back to the end of the file
            _fileStream.Seek(0, SeekOrigin.End);
        }

        /// <summary>
        /// Write data to WAV file. This function works only with 8bit samples.
        /// </summary>
        /// <param name="buffer">Pointer to sample data buffer.</param>
        /// <param name="numElements">How many array items are to be written to
        /// file.</param>
        /// <exception cref="InvalidOperationException">Error: 
        /// <see cref="Write(byte[], int)"/> accepts only 8bit samples.
        /// </exception>
        public void Write(byte[] buffer, int numElements)
        {
            if (_header.Format.BitsPerSample != 8)
            {
                throw new InvalidOperationException("Error: WavOutFile.Write(byte[], int) accepts only 8bit samples.");
            }

            _fileStream.Write(buffer, 0, numElements);

            _bytesWritten += numElements;
        }

        /// <summary>
        /// Write data to WAV file.
        /// </summary>
        /// <param name="buffer">Pointer to sample data buffer.</param>
        /// <param name="numElements">How many array items are to be written to
        /// file.</param>
        /// <exception cref="InvalidOperationException">WAV file bits per sample
        /// format not supported.</exception>
        public void Write(short[] buffer, int numElements)
        {
            // 16 bit samples
            if (numElements < 1) return; // nothing to do

            if (_header.Format.BitsPerSample == 8)
            {
                int i;
                var temp = new byte[numElements];
                // convert from 16bit format to 8bit format
                for (i = 0; i < numElements; i ++)
                {
                    temp[i] = (byte) (buffer[i] >> 8);
                }
                // write in 8bit format
                Write(temp, numElements);
            }
            else
            {
                // 16bit format
                var pTemp = new byte[numElements*sizeof (short)];

                if (_header.Format.BitsPerSample != 16)
                {
                    string msg = string.Format("WAV file bits per sample format not supported: {0} bits per sample.", _header.Format.BitsPerSample);
                    throw new InvalidOperationException(msg);
                }

                // allocate temp buffer to swap byte order if necessary
                _endian.Swap16Buffer(buffer, numElements);
                Buffer.BlockCopy(buffer, 0, pTemp, 0, numElements*sizeof (short));

                _fileStream.Write(pTemp, 0, numElements*sizeof (short));


                _bytesWritten += 2*numElements;
            }
        }

        /// <summary>
        /// Write data to WAV file in floating point format, saturating sample values to range [-1..+1].
        /// </summary>
        /// <param name="buffer">Pointer to sample data buffer.</param>
        /// <param name="numElements">How many array items are to be written to file.</param>
        public void Write(float[] buffer, int numElements)
        {
            var temp = new short[numElements];

            // convert to 16 bit integer
            for (int i = 0; i < numElements; i++)
            {
                // convert to integer
                var iTemp = (int) (32768.0f*buffer[i]);

                // saturate
                if (iTemp < -32768) iTemp = -32768;
                if (iTemp > 32767) iTemp = 32767;
                temp[i] = (short) iTemp;
            }

            Write(temp, numElements);
        }
    } ;
}