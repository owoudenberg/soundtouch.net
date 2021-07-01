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
    using NAudio.Wave;
    using SoundTouch.Net.NAudioSupport.Assets;

    /// <summary>
    /// Wave Stream for applying <see cref="SoundTouch"/> effects on the
    /// concents of a <see cref="WaveStream"/>.
    /// </summary>
    public class SoundTouchWaveStream : WaveStream
    {
        private readonly SoundTouchWaveProvider _provider;
        private WaveStream? _sourceStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundTouchWaveStream"/> class.
        /// </summary>
        /// <param name="sourceStream">Input stream.</param>
        public SoundTouchWaveStream(WaveStream sourceStream)
            : this(sourceStream, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundTouchWaveStream"/> class.
        /// </summary>
        /// <param name="sourceStream">Input stream.</param>
        /// <param name="processor">The processor for changing tempo, pitch and rate. When not specified, a new instance of <see cref="SoundTouchProcessor"/> is used.</param>
        public SoundTouchWaveStream(WaveStream sourceStream, SoundTouchProcessor? processor)
        {
            _sourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));
            _provider = new SoundTouchWaveProvider(sourceStream, processor);
        }

        /// <inheritdoc/>
        public override WaveFormat WaveFormat => _provider.WaveFormat;

        /// <inheritdoc/>
        public override long Length => SourceStream.Length;

        /// <inheritdoc/>
        public override bool CanRead => !(_sourceStream is null) && base.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => !(_sourceStream is null) && base.CanSeek;

        /// <summary>
        /// Gets or sets the tempo control value.
        /// </summary>
        public double Tempo
        {
            get => _provider.Tempo;
            set => _provider.Tempo = value;
        }

        /// <summary>
        /// Gets or sets pitch control value. Original pitch = 1.0, smaller values
        /// represent lower pitches, larger values higher pitch.
        /// </summary>
        public double Pitch
        {
            get => _provider.Pitch;
            set => _provider.Pitch = value;
        }

        /// <summary>
        /// Gets or sets the rate control value.
        /// </summary>
        /// <value>Normal rate = 1.0, smaller values represent slower rate,
        /// larger faster rates.</value>
        public double Rate
        {
            get => _provider.Rate;
            set => _provider.Rate = value;
        }

        /// <summary>
        /// Gets or sets new tempo control value as a difference in percents compared
        /// to the original tempo (-50 .. +100 %).
        /// </summary>
        public double TempoChange
        {
            get => _provider.TempoChange;
            set => _provider.TempoChange = value;
        }

        /// <summary>
        /// Gets or sets pitch change in octaves compared to the original pitch
        /// (-1.00 .. +1.00).
        /// </summary>
        public double PitchOctaves
        {
            get => _provider.PitchOctaves;
            set => _provider.PitchOctaves = value;
        }

        /// <summary>
        /// Gets or sets pitch change in semi-tones compared to the original pitch
        /// (-12 .. +12).
        /// </summary>
        public double PitchSemiTones
        {
            get => _provider.PitchSemiTones;
            set => _provider.PitchSemiTones = value;
        }

        /// <summary>
        /// Gets or sets the rate control value as a difference in percents compared
        /// to the original rate (-50 .. +100 %).
        /// </summary>
        public double RateChange
        {
            get => _provider.RateChange;
            set => _provider.RateChange = value;
        }

        /// <inheritdoc/>
        public override long Position
        {
            get => SourceStream.Position;
            set
            {
                lock (_provider.SyncLock)
                {
                    SourceStream.Position = value;
                    _provider.Clear();
                }
            }
        }

        private WaveStream SourceStream
        {
            get
            {
                if (_sourceStream is null)
                    throw new ObjectDisposedException(null, Strings.ObjectDisposed_StreamClosed);

                return _sourceStream;
            }
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => _provider.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override void Flush()
        {
            _provider.Clear();
            base.Flush();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _sourceStream != null)
                {
                    try
                    {
                        _sourceStream.Flush();
                    }
                    finally
                    {
                        _sourceStream.Dispose();
                    }
                }
            }
            finally
            {
                _sourceStream = null;
                base.Dispose(disposing);
            }
        }
    }
}
