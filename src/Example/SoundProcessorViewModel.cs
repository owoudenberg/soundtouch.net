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
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using SoundTouch;

    public sealed class SoundProcessorViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SoundProcessor _processor;

        private int _tempo;
        private int _pitch;
        private int _rate;
        private string? _status;
        private string? _filename;

        public SoundProcessorViewModel()
        {
            StatusMessage.StatusEvent += (_, message) => Status = message;

            _processor = new SoundProcessor();
            _processor.PlaybackStopped += OnPlaybackStopped;

            Browse = new Command(OnBrowse);
            Play = new Command(OnPlay);
            Pause = new Command(OnPause);
            Stop = new Command(OnStop);

            SetPlaybackMode(PlaybackMode.Unloaded);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private enum PlaybackMode
        {
            Unloaded,
            Stopped,
            Playing,
            Paused,
        }

        public string SoundTouchVersion { get; } = $"SoundTouch version: {SoundTouchProcessor.Version}";

        public Command Browse { get; }

        public Command Play { get; }

        public Command Pause { get; }

        public Command Stop { get; }

        public string? Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public int Tempo
        {
            get => _tempo;
            set
            {
                Set(ref _tempo, value);

                if (_processor.ProcessorStream != null)
                    _processor.ProcessorStream.TempoChange = value;
            }
        }

        public int Pitch
        {
            get => _pitch;
            set
            {
                Set(ref _pitch, value);
                if (_processor.ProcessorStream != null)
                    _processor.ProcessorStream.PitchSemiTones = value;
            }
        }

        public int Rate
        {
            get => _rate;
            set
            {
                Set(ref _rate, value);
                if (_processor.ProcessorStream != null)
                    _processor.ProcessorStream.RateChange = value;
            }
        }

        public string? Filename
        {
            get => _filename;
            set => Set(ref _filename, value);
        }

        public void OnDropFiles(string[] files)
        {
            string? filename = System.Array.Find(files, f => !string.IsNullOrEmpty(f));
            if (filename != null)
                OpenFile(filename);
        }

        public void Dispose()
        {
            _processor.Dispose();
        }

        private void OnBrowse()
        {
            // Show file selection dialog
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            if (!string.IsNullOrEmpty(Filename))
            {
                // if an audio file is open, set directory to same as with the file
                openDialog.InitialDirectory = Path.GetDirectoryName(Filename);
            }

            openDialog.Filter = "MP3 files (*.mp3)|*.mp3|Audio files (*.wav)|*.wav";
            if (openDialog.ShowDialog() == true)
            {
                OpenFile(openDialog.FileName);
            }
        }

        private void OpenFile(string filename)
        {
            OnStop();
            if (_processor.OpenFile(filename))
            {
                Filename = filename;
                SetPlaybackMode(PlaybackMode.Stopped);
            }
            else
            {
                Filename = string.Empty;
                SetPlaybackMode(PlaybackMode.Unloaded);

                MessageBox.Show($"Couldn't open audio file {filename}");
            }
        }

        private void OnPlay()
        {
            if (_processor.Play())
            {
                SetPlaybackMode(PlaybackMode.Playing);
            }
        }

        private void OnPause()
        {
            if (_processor.Pause())
            {
                SetPlaybackMode(PlaybackMode.Paused);
            }
        }

        private void OnStop()
        {
            if (_processor.Stop())
            {
                SetPlaybackMode(PlaybackMode.Stopped);
            }
        }

        private void SetPlaybackMode(PlaybackMode mode)
        {
            Stop.IsEnabled = mode == PlaybackMode.Playing || mode == PlaybackMode.Paused;
            Play.IsEnabled = mode == PlaybackMode.Stopped || mode == PlaybackMode.Paused;
            Pause.IsEnabled = mode == PlaybackMode.Playing;

            Status = mode switch
            {
                PlaybackMode.Stopped => "Stopped",
                PlaybackMode.Playing => "Playing",
                PlaybackMode.Paused => "Paused",
                _ => string.Empty
            };
        }

        private void OnPlaybackStopped(object? sender, bool endReached)
        {
            if (endReached)
            {
                SetPlaybackMode(PlaybackMode.Stopped);
            }
            else
            {
                SetPlaybackMode(PlaybackMode.Paused);
            }
        }

        private void Set<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return;

            storage = value;

            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
