// License :
//
// SoundTouch audio processing library
// Copyright (c) Olli Parviainen
// C# port Copyright (c) Olaf Woudenberg
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

namespace Example
{
    using System;
    using System.IO;
    using NAudio.Wave;
    using SoundTouch.Net.NAudioSupport;

    /// <summary>
    /// Class that opens & plays MP3 file and allows real-time audio processing with SoundTouch
    /// while playing.
    /// </summary>
    public sealed class SoundProcessor : IDisposable
    {
        private IWavePlayer? _waveOut;

        /// <summary>
        /// Event for "playback stopped" event. 'bool' argument is true if playback has reached end of stream.
        /// </summary>
        public event EventHandler<bool> PlaybackStopped = (_, __) => { };

        public SoundTouchWaveStream? ProcessorStream { get; private set; }

        /// <summary>
        /// Start / resume playback.
        /// </summary>
        /// <returns>true if successful, false if audio file not open.</returns>
        public bool Play()
        {
            if (_waveOut is null)
                return false;

            if (_waveOut.PlaybackState != PlaybackState.Playing)
            {
                _waveOut.Play();
            }

            return true;
        }

        /// <summary>
        /// Pause playback.
        /// </summary>
        /// <returns>true if successful, false if audio not playing.</returns>
        public bool Pause()
        {
            if (_waveOut is null)
                return false;

            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Stop();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Stop playback.
        /// </summary>
        /// <returns>true if successful, false if audio file not open.</returns>
        public bool Stop()
        {
            if (_waveOut is null || ProcessorStream is null)
                return false;

            _waveOut.Stop();
            ProcessorStream.Position = 0;
            ProcessorStream.Flush();
            return true;
        }

        /// <summary>
        /// Opens the specfiied filename for playback.
        /// </summary>
        /// <param name="filePath">Path to file to open.</param>
        /// <returns><see langword=""="true"/> if successful; otherwise <see langword=""="false"/>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reviewed: The WaveStreams are disposed by the SoundTouchWaveStream.")]
        public bool OpenFile(string filePath)
        {
            Close();

            try
            {
                var reader = Path.GetExtension(filePath) == ".mp3"
                    ? new Mp3FileReader(filePath)
                    : (WaveStream)new WaveFileReader(filePath);

                // don't pad, otherwise the stream never ends
                var inputStream = new WaveChannel32(reader) { PadWithZeroes = false };

                ProcessorStream = new SoundTouchWaveStream(inputStream);

                _waveOut = new WaveOutEvent() { DesiredLatency = 100 };

                _waveOut.Init(ProcessorStream);
                _waveOut.PlaybackStopped += OnPlaybackStopped;

                StatusMessage.Write("Opened file " + filePath);
                return true;
            }
            catch (Exception exp)
            {
                // Error in opening file
                _waveOut = null;
                StatusMessage.Write("Can't open file: " + exp.Message);
                return false;
            }
        }

        public void Close()
        {
            ProcessorStream?.Dispose();
            ProcessorStream = null;

            _waveOut?.Dispose();
            _waveOut = null;
        }

        public void Dispose() => Close();

        /// <summary>
        /// Proxy event handler for receiving playback stopped event from WaveOut.
        /// </summary>
        private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
        {
            bool reachedEnding = ProcessorStream is null || ProcessorStream.Position >= ProcessorStream.Length;
            if (reachedEnding)
            {
                Stop();
            }

            PlaybackStopped(sender, reachedEnding);
        }
    }
}
