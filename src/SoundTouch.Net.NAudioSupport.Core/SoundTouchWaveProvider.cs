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

namespace SoundTouch.Net.NAudioSupport
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using NAudio.Wave;
    using SoundTouch.Net.NAudioSupport.Assets;

    /// <summary>
    /// Wave provider for controlling the tempo, pitch and rate.
    /// </summary>
    public class SoundTouchWaveProvider : IWaveProvider
    {
        private readonly IWaveProvider _sourceProvider;
        private readonly SoundTouchProcessor _processor;
        private readonly byte[] _buffer = new byte[4096];

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundTouchWaveProvider"/> class.
        /// </summary>
        /// <param name="sourceProvider">The source provider.</param>
        /// <param name="processor">The processor for changing tempo, pitch and rate. When not specified, a new instance of <see cref="SoundTouchProcessor"/> is used.</param>
        public SoundTouchWaveProvider(IWaveProvider sourceProvider, SoundTouchProcessor? processor = null)
        {
            _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));

            if (sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                throw new ArgumentException(Strings.Argument_WaveFormatIeeeFloat, nameof(sourceProvider));
            if (sourceProvider.WaveFormat.BitsPerSample != 32)
                throw new ArgumentException(Strings.Argument_WaveFormat32BitsPerSample, nameof(sourceProvider));

            int sampleRate = sourceProvider.WaveFormat.SampleRate;
            int channels = sourceProvider.WaveFormat.Channels;

            _processor = processor ?? new SoundTouchProcessor();
            _processor.SampleRate = sampleRate;
            _processor.Channels = channels;

            if (processor is null)
            {
                _processor.Tempo = 1.0;
                _processor.Pitch = 1.0;
                _processor.Rate = 1.0;
            }
        }

        /// <summary>
        /// Occurs when an exception is caught during a read operation.
        /// </summary>
        public event EventHandler<UnobservedExceptionEventArgs> UnobservedException = (_, __) => { };

        /// <inheritdoc/>
        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        /// <summary>
        /// Gets or sets the tempo control value.
        /// </summary>
        public double Tempo
        {
            get => _processor.Tempo;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.Tempo, value))
                        return;

                    _processor.Tempo = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets pitch control value. Original pitch = 1.0, smaller values
        /// represent lower pitches, larger values higher pitch.
        /// </summary>
        public double Pitch
        {
            get => _processor.Pitch;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.Pitch, value))
                        return;

                    _processor.Pitch = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the rate control value.
        /// </summary>
        /// <value>Normal rate = 1.0, smaller values represent slower rate,
        /// larger faster rates.</value>
        public double Rate
        {
            get => _processor.Rate;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.Rate, value))
                        return;

                    _processor.Rate = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets new tempo control value as a difference in percents compared
        /// to the original tempo (-50 .. +100 %).
        /// </summary>
        public double TempoChange
        {
            get => _processor.TempoChange;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.TempoChange, value))
                        return;

                    _processor.TempoChange = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets pitch change in octaves compared to the original pitch
        /// (-1.00 .. +1.00).
        /// </summary>
        public double PitchOctaves
        {
            get => _processor.PitchOctaves;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.PitchOctaves, value))
                        return;

                    _processor.PitchOctaves = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets pitch change in semi-tones compared to the original pitch
        /// (-12 .. +12).
        /// </summary>
        public double PitchSemiTones
        {
            get => _processor.PitchSemiTones;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.PitchSemiTones, value))
                        return;

                    _processor.PitchSemiTones = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the rate control value as a difference in percents compared
        /// to the original rate (-50 .. +100 %).
        /// </summary>
        public double RateChange
        {
            get => _processor.RateChange;
            set
            {
                lock (SyncLock)
                {
                    if (DoubleUtil.AreClose(_processor.RateChange, value))
                        return;

                    _processor.RateChange = value;
                }
            }
        }

        internal bool IsFlushed { get; private set; }

        internal object SyncLock { get; } = new object();

        /// <summary>
        /// Change the settings of the sound processing to be better suited for natural speech.
        /// </summary>
        public void OptimizeForSpeech()
        {
            // Change settings to optimize for Speech.
            _processor.SetSetting(SettingId.SequenceDurationMs, 50);
            _processor.SetSetting(SettingId.SeekWindowDurationMs, 10);
            _processor.SetSetting(SettingId.OverlapDurationMs, 20);
            _processor.SetSetting(SettingId.UseQuickSeek, 0);
        }

        /// <summary>
        /// Clears all the samples buffered in the internal processing.
        /// </summary>
        public void Clear()
        {
            lock (SyncLock)
            {
                _processor.Clear();
                IsFlushed = false;
            }
        }

        /// <inheritdoc/>
        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesRequired = count / sizeof(float);

            try
            {
                lock (SyncLock)
                {
                    // Iterate until enough samples are available for output:
                    // - read samples from input stream
                    // - put samples to SoundTouch processor.
                    while (_processor.AvailableSamples < samplesRequired)
                    {
                        int bytes;
                        try
                        {
                            bytes = _sourceProvider.Read(_buffer, 0, _buffer.Length);
                        }
                        catch (EndOfStreamException)
                        {
                            bytes = 0;
                        }

                        if (bytes == 0)
                        {
                            // end of stream. flush final samples from SoundTouch buffers to output.
                            if (!IsFlushed)
                            {
                                IsFlushed = true;
                                _processor.Flush();
                            }

                            break;
                        }

                        var ptr = MemoryMarshal.Cast<byte, float>(_buffer.AsSpan().Slice(0, bytes));
                        _processor.PutSamples(ptr, ptr.Length / WaveFormat.Channels);
                    }

                    // get processed out samples from the SoundTouch processor.
                    var output = MemoryMarshal.Cast<byte, float>(buffer.AsSpan().Slice(offset, count));
                    output.Clear();

                    int samplesRead = _processor.ReceiveSamples(output, output.Length / WaveFormat.Channels);

                    return samplesRead * sizeof(float) * WaveFormat.Channels;
                }
            }
            catch (Exception exception)
            {
                var eventArgs = new UnobservedExceptionEventArgs(exception);
                UnobservedException(this, eventArgs);

                if (!eventArgs.IsObserved)
                    throw;

                return 0;
            }
        }
    }
}
