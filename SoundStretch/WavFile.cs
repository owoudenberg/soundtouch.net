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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SoundStretch
{
    /// <summary>WAV audio file 'riff' section header</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WavRiff
    {
        public const string RIFF_STR = "RIFF";
        public const string WAVE_STR = "WAVE";
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)] 
        public char[] Riff;
        public int PackageLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)] 
        public char[] Wave;
    }

    /// <summary>WAV audio file 'format' section header</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WavFormat
    {
        public const string FMT_STR = "fmt ";
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)] 
        public char[] Fmt;
        public int FormatLen;
        public short Fixed;
        public short ChannelNumber;
        public int SampleRate;
        public int ByteRate;
        public short BytePerSample;
        public short BitsPerSample;
    }

    /// <summary>WAV audio file 'data' section header</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WavData
    {
        public const string DATA_STR = "data";
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)] 
        public char[] DataField;
        public int DataLen;
    }

    /// <summary>WAV audio file header</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WavHeader
    {
        public WavRiff Riff;
        public WavFormat Format;
        public WavData Data;
    }

    /// <summary>
    /// Classes for easy reading & writing of WAV sound files. 
    ///
    /// Admittingly, more complete WAV reader routines may exist in public domain,
    /// but the reason for 'yet another' one is that those generic WAV reader 
    /// libraries are exhaustingly large and cumbersome! Wanted to have something
    /// simpler here, i.e. something that's not already larger than rest of the
    /// <c>SoundTouch</c>/<c>SoundStretch</c> program...
    /// </summary>
    public class WavInFile : IDisposable
    {
        private static readonly EndianHelper _endian = EndianHelper.NewInstance();

        private Stream _fileStream;

        /// <summary>Counter of how many bytes of sample data have been read from the file.</summary>
        private long _dataRead;


        /// <summary>WAV header information</summary>
        private WavHeader _header;

        /// <summary>Opens the given WAV file.</summary>
        /// <exception cref="ArgumentException">the file can't be opened.</exception>
        public WavInFile(string filename)
        {
            // Try to open the file for reading
            try
            {
                _fileStream = File.Open(filename, FileMode.Open, FileAccess.Read);
            }
            catch (Exception exception)
            {
                // didn't succeed
                string msg = string.Format("Error : Unable to open file \"{0}\" for reading.", filename);
                throw new ArgumentException(msg, exception);
            }

            Init();
        }

        /// <exception cref="ArgumentException">Unable to access input stream
        /// for reading.</exception>
        public WavInFile(Stream fileStream)
        {
            _fileStream = fileStream;
            if (_fileStream == null)
            {
                // didn't succeed
                const string msg = "Error : Unable to access input stream for reading";
                throw new ArgumentException(msg);
            }

            Init();
        }

        /// Closes the file.
        public void Dispose()
        {
            if (_fileStream != null)
                _fileStream.Dispose();
            _fileStream = null;
        }

        /// <summary>Init the WAV file stream</summary>
        /// <exception cref="InvalidOperationException">Input file is corrupt,
        /// not a WAV file or uses unsupported encoding</exception>
        private void Init()
        {
            // assume file stream is already open
            Debug.Assert(_fileStream != null);

            // Read the file headers
            int hdrsOk = ReadWavHeaders();
            if (hdrsOk != 0)
            {
                // Something didn't match in the wav file headers 
                const string msg = "Input file is corrupt or not a WAV file";
                throw new InvalidOperationException(msg);
            }

            if (_header.Format.Fixed != 1)
            {
                const string msg = "Input file uses unsupported encoding.";
                throw new InvalidOperationException(msg);
            }

            _dataRead = 0;
        }

        /// <summary>Read WAV file headers.</summary>
        /// <returns>zero if all ok, nonzero if file format is invalid.
        /// </returns>
        private int ReadWavHeaders()
        {
            _header = new WavHeader();

            int res = ReadRiffBlock();
            if (res != 0) return -1;
            // read header blocks until data block is found
            do
            {
                // read header blocks
                res = ReadHeaderBlock();
                if (res < 0) return -1; // error in file structure
            } while (res == 0);
            // check that all required tags are legal
            return CheckCharTags();
        }

        /// <summary>
        /// Checks WAV file header tags.
        /// </summary>
        /// <returns>zero if all ok, nonzero if file format is invalid.
        /// </returns>
        private int CheckCharTags()
        {
            // header.format.fmt should equal to 'fmt '
            if (!WavFormat.FMT_STR.SequenceEqual(_header.Format.Fmt)) return -1;
            // header.data.data_field should equal to 'data'
            if (!WavData.DATA_STR.SequenceEqual(_header.Data.DataField)) return -1;

            return 0;
        }

        /// <summary>
        /// Reads a single WAV file header block.
        /// </summary>
        /// <returns><c>true</c> if all ok, <c>false</c> if file format is
        /// invalid.</returns>
        private int ReadHeaderBlock()
        {
            // lead label string
            string label;
            if (_fileStream.Read(out label, 4) != 4) return -1;

            if (!IsAlphaStr(label)) return -1; // not a valid label

            // Decode blocks according to their label
            if (label == WavFormat.FMT_STR)
            {

                // read length of the format field
                int nLen;
                if (_fileStream.Read(out nLen) != sizeof (int)) return -1;

                // swap byte order if necessary
                _endian.Swap32(ref nLen);

                // calculate how much length differs from expected
                int nDump = nLen - (Marshal.SizeOf(_header.Format) - 8);

                // if format_len is larger than expected, read only as much data as we've space for
                if (nDump > 0)
                {
                    nLen = Marshal.SizeOf(_header.Format) - 8;
                }

                // read data
                const int skip = (sizeof(byte) * 4) + sizeof (int);
                var data = new byte[skip + nLen];
                if (_fileStream.Read(data, skip, nLen) != nLen) return -1;

                // swap byte order if necessary
                GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    _header.Format = (WavFormat)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(WavFormat));                    
                }
                finally
                {
                    handle.Free();
                }

                // 'fmt ' block 
                _header.Format.Fmt = WavFormat.FMT_STR.ToArray();
                _header.Format.FormatLen = nLen;

                _endian.Swap16(ref _header.Format.Fixed);
                _endian.Swap16(ref _header.Format.ChannelNumber);
                _endian.Swap32(ref _header.Format.SampleRate);
                _endian.Swap32(ref _header.Format.ByteRate);
                _endian.Swap16(ref _header.Format.BytePerSample);
                _endian.Swap16(ref _header.Format.BitsPerSample);

                // if format_len is larger than expected, skip the extra data
                if (nDump > 0)
                {
                    _fileStream.Seek(nDump, SeekOrigin.Current);
                }

                return 0;
            }

            if (label == WavData.DATA_STR)
            {
                // 'data' block
                _header.Data.DataField = WavData.DATA_STR.ToCharArray();
                if (_fileStream.Read(out _header.Data.DataLen) != sizeof (int)) return -1;

                // swap byte order if necessary
                _endian.Swap32(ref _header.Data.DataLen);

                return 1;
            }

            // unknown block
            // read length
            int len;
            if (_fileStream.Read(out len) != sizeof (int)) return -1;

            // scan through the block
            for (int i = 0; i < len; i ++)
            {
                if ((_fileStream.ReadByte()) < 0) return -1;
                if (_fileStream.Position == _fileStream.Length) return -1; // unexpected eof
            }
            return 0;
        }

        /// <summary>test if character code is between a white space ' ' and little 'z'</summary>
        private static bool IsAlpha(char c)
        {
            return (c >= ' ' && c <= 'z');
        }
        
        /// <summary>test if all characters are between a white space ' ' and little 'z'</summary>
        private static bool IsAlphaStr(IEnumerable<char> str)
        {
            return str.All(IsAlpha);
        }

        /// <summary>
        /// Reads WAV file 'riff' block
        /// </summary>
        private int ReadRiffBlock()
        {
            int size = Marshal.SizeOf(typeof (WavRiff));
            var buffer = new byte[size];
            if (_fileStream.Read(buffer, 0, size) != size) return -1;
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                _header.Riff = (WavRiff) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof (WavRiff));
            }
            finally
            {
                handle.Free();
            }

            // swap 32bit data byte order if necessary
            _endian.Swap32(ref _header.Riff.PackageLength);

            // _header.Riff.Riff should equal to 'RIFF');
            if (!WavRiff.RIFF_STR.SequenceEqual(_header.Riff.Riff)) return -1;
            // _header.Riff.Wave should equal to 'WAVE'
            if (!WavRiff.WAVE_STR.SequenceEqual(_header.Riff.Wave)) return -1;

            return 0;
        }

        /// <summary>
        /// Rewind to beginning of the file.
        /// </summary>
        public void Rewind()
        {
            _fileStream.Seek(0, SeekOrigin.Begin);
            int hdrsOk = ReadWavHeaders();
            Debug.Assert(hdrsOk == 0);
            _dataRead = 0;
        }

        /// <summary>
        /// Gets the sample rate.
        /// </summary>
        public int GetSampleRate()
        {
            return _header.Format.SampleRate;
        }

        /// <summary>
        /// Get number of bits per sample, i.e. 8 or 16.
        /// </summary>
        public int GetNumBits()
        {
            return _header.Format.BitsPerSample;
        }

        /// <summary>
        /// Get sample data size in bytes. Ahem, this should return same
        /// information as <see cref="GetBytesPerSample"/>...
        /// </summary>
        public int GetDataSizeInBytes()
        {
            return _header.Data.DataLen;
        }

        /// <summary>
        /// Get total number of samples in file.
        /// </summary>
        public int GetNumSamples()
        {
            if (_header.Format.BytePerSample == 0) return 0;
            return _header.Data.DataLen/_header.Format.BytePerSample;
        }

        /// <summary>
        /// Get number of bytes per audio sample (e.g. 16bit stereo = 4
        /// bytes/sample)
        /// </summary>
        public int GetBytesPerSample()
        {
            return GetNumChannels()*GetNumBits()/8;
        }

        /// <summary>
        /// Get number of audio channels in the file (1=mono, 2=stereo)
        /// </summary>
        public int GetNumChannels()
        {
            return _header.Format.ChannelNumber;
        }

        /// <summary>
        /// Get the audio file length in milliseconds.
        /// </summary>
        public int GetLengthMs()
        {
            int numSamples = GetNumSamples();
            int sampleRate = GetSampleRate();

            Debug.Assert(numSamples < int.MaxValue/1000);
            return (1000*numSamples/sampleRate);
        }

        /// <summary>
        /// Reads audio samples from the WAV file. This routine works only for 8
        /// bit samples. Reads given number of elements from the file or if
        /// end-of-file reached, as many elements as are left in the file.
        /// </summary>
        /// <param name="buffer">Pointer to buffer where to read data.</param>
        /// <param name="maxElements">Size of 'buffer' array (number of array
        /// elements).</param>
        /// <returns>
        /// Number of 8-bit integers read from the file.
        /// </returns>
        /// <exception cref="InvalidOperationException">Error:
        /// <see cref="Read(byte[],int)"/> works only with 8bit samples.
        /// </exception>
        public int Read(byte[] buffer, int maxElements)
        {
            // ensure it's 8 bit format
            if (_header.Format.BitsPerSample != 8)
            {
                throw new InvalidOperationException("Error: WavInFile.Read(byte[], int) works only with 8bit samples.");
            }
            Debug.Assert(sizeof (byte) == 1);

            int numBytes = maxElements;
            long afterDataRead = _dataRead + numBytes;
            if (afterDataRead > _header.Data.DataLen)
            {
                // Don't read more samples than are marked available in header
                numBytes = _header.Data.DataLen - (int) _dataRead;
                Debug.Assert(numBytes >= 0);
            }

            Debug.Assert(buffer != null);
            numBytes = _fileStream.Read(buffer, 0, numBytes);
            _dataRead += numBytes;

            return numBytes;
        }

        /// <summary>
        /// Reads audio samples from the WAV file to 16 bit integer format.
        /// Reads given number of elements from the file or if end-of-file
        /// reached, as many elements as are left in the file.
        /// </summary>
        /// <param name="buffer">Pointer to buffer where to read data.</param>
        /// <param name="maxElements">Size of 'buffer' array (number of array
        /// elements).</param>
        /// <returns>Number of 16-bit integers read from the file.</returns>
        /// <exception cref="InvalidOperationException">WAV file bits per sample
        /// format not supported.</exception>
        public int Read(short[] buffer, int maxElements)
        {
            int numElems;

            Debug.Assert(buffer != null);
            if (_header.Format.BitsPerSample == 8)
            {
                // 8 bit format
                var temp = new byte[maxElements];
                int i;

                numElems = Read(temp, maxElements);
                // convert from 8 to 16 bit
                for (i = 0; i < numElems; i ++)
                {
                    buffer[i] = (short) (temp[i] << 8);
                }
            }
            else
            {
                // 16 bit format
                if (_header.Format.BitsPerSample != 16)
                {
                    string msg = string.Format("Only 8/16 bit sample WAV files supported. Can't open WAV file with {0} bit sample format.", (int)_header.Format.BitsPerSample);
                    throw new InvalidOperationException(msg);
                }

                Debug.Assert(sizeof (short) == 2);

                int numBytes = maxElements*2;
                long afterDataRead = _dataRead + numBytes;
                if (afterDataRead > _header.Data.DataLen)
                {
                    // Don't read more samples than are marked available in header
                    numBytes = _header.Data.DataLen - (int) _dataRead;
                    Debug.Assert(numBytes >= 0);
                }

                var data = new byte[buffer.Length*sizeof (short)];
                numBytes = _fileStream.Read(data, 0, numBytes);
                Buffer.BlockCopy(data, 0, buffer, 0, numBytes);

                _dataRead += numBytes;
                numElems = numBytes/2;

                // 16bit samples, swap byte order if necessary
                _endian.Swap16Buffer(buffer, numElems);
            }

            return numElems;
        }

        /// <summary>
        /// Reads audio samples from the WAV file to floating point format,
        /// converting sample values to range [-1,1[. Reads given number of
        /// elements from the file or if end-of-file reached, as many elements
        /// as are left in the file.
        /// </summary>
        /// <param name="buffer">Pointer to buffer where to read data.</param>
        /// <param name="maxElements">Size of <paramref name="buffer"/> array
        /// (number of array elements).</param>
        /// <returns>Number of elements read from the file.</returns>
        public int Read(float[] buffer, int maxElements)
        {
            var temp = new short[maxElements];

            int num = Read(temp, maxElements);

            const double fscale = 1.0/32768.0;
            // convert to floats, scale to range [-1..+1[
            for (int i = 0; i < num; i++)
            {
                buffer[i] = (float) (fscale*temp[i]);
            }

            return num;
        }

        /// <summary>
        /// Check end-of-file.
        /// </summary>
        /// <returns><c>true</c> if end-of-file reached.</returns>
        public bool Eof()
        {
            return _dataRead == _header.Data.DataLen || _fileStream.Position >= _fileStream.Length;
        }
    }
}