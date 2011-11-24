using System;
using System.IO;
using System.Text;

namespace SoundStretch
{
    /// <summary>
    /// Extension methods working on a Stream object.
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Reads a string from the specified stream.
        /// </summary>
        public static int Read(this Stream stream, out string value, int count)
        {
            var data = new byte[count];
            int read = stream.Read(data, 0, count);
            value = Encoding.ASCII.GetString(data, 0, read);
            return read;
        }

        /// <summary>
        /// Reads an integer from the specified stream.
        /// </summary>
        public static int Read(this Stream stream, out int value)
        {            
            var data = new byte[sizeof(int)];
            int read = stream.Read(data, 0, sizeof (int));
            value = BitConverter.ToInt32(data, 0);
            return read;
        }
    }
}