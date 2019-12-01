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

namespace SoundStretch
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using NAudio.Wave;
    using SoundTouch;
    using SoundTouch.Net.NAudioSupport;

    public static class Program
    {
        // Processing chunk size (size chosen to be divisible by 2, 4, 6, 8, 10, 12, 14, 16 channels ...)
        private const int BUFF_SIZE = 6720;

        private const string HELLO_TEXT = @"
   SoundStretch v{0}
      C++ Version Written by Olli Parviainen 2001 - 2019
   SoundStretch v{0}
      C# Version Written by Olaf Woudenberg 2011 - 2019
=========================================================

This program is subject to (L)GPL license. Run ""soundstretch -license"" for
more information.";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Reviewed.")]
        public static int Main(string[] args)
        {
            Console.Error.WriteLine(HELLO_TEXT, SoundTouchProcessor.VersionString);

            try
            {
                var parameters = new RunParameters(args);
                var processor = new SoundTouchProcessor();

                OpenFiles(out var inputFile, out var outputFile, parameters);

                using (inputFile)
                using (outputFile)
                {
                    if (parameters.DetectBpm)
                    {
                        DetectBpm(inputFile, parameters);
                    }

                    // Setup the 'SoundTouch' object for processing the sound.
                    Setup(processor, inputFile, parameters);

                    var stopwatch = Stopwatch.StartNew();

                    // Process the sound.
                    Process(processor, inputFile, outputFile);
                    Debug.WriteLine("Duration: {0}", stopwatch.Elapsed);

                    Console.Error.WriteLine("Done!");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                return -1;
            }

            return 0;
        }

        private static void OpenFiles(out WaveFileReader inputFile, out Stream? outputFile, in RunParameters parameters)
        {
            inputFile = string.Equals(parameters.InFileName, "stdin", StringComparison.Ordinal)
                ? new WaveFileReader(Console.OpenStandardInput())
                : new WaveFileReader(parameters.InFileName);

            if (!string.IsNullOrEmpty(parameters.OutFileName))
            {
                outputFile = string.Equals(parameters.OutFileName, "stdout", StringComparison.Ordinal)
                    ? Console.OpenStandardOutput()
                    : File.Create(parameters.OutFileName);
            }
            else
            {
                outputFile = null;
            }
        }

        private static void Setup(SoundTouchProcessor processor, WaveFileReader inputFile, RunParameters parameters)
        {
            var sampleRate = inputFile.WaveFormat.SampleRate;
            var channels = inputFile.WaveFormat.Channels;

            processor.SampleRate = sampleRate;
            processor.Channels = channels;

            processor.TempoChange = parameters.TempoDelta;
            processor.PitchSemiTones = parameters.PitchDelta;
            processor.RateChange = parameters.RateDelta;

            processor.SetSetting(SettingId.UseQuickSeek, parameters.Quick ? 1 : 0);
            processor.SetSetting(SettingId.UseAntiAliasFilter, parameters.NoAntiAlias ? 0 : 1);

            if (parameters.Speech)
            {
                // use settings for speech processing
                processor.SetSetting(SettingId.SequenceDurationMs, 40);
                processor.SetSetting(SettingId.SeekWindowDurationMs, 15);
                processor.SetSetting(SettingId.OverlapDurationMs, 8);
                Console.Error.WriteLine("Tune processing parameters for speech processing.");
            }

            // print processing information
            if (parameters.OutFileName != null)
            {
                Console.Error.WriteLine("Uses 32bit floating point sample type in processing.");

                // print processing information only if outFileName given i.e. some processing will happen
                Console.Error.WriteLine("Processing the file with the following changes:");
                Console.Error.WriteLine("  tempo change = {0:0.00} %", parameters.TempoDelta);
                Console.Error.WriteLine("  pitch change = {0} semitones", parameters.PitchDelta);
                Console.Error.WriteLine("  rate change  = {0:0.00} %", parameters.RateDelta);
                Console.Error.WriteLine("Working...");
            }
            else
            {
                // outFileName not given
                Console.Error.WriteLine("Warning: output file name missing, won't output anything.");
            }

            Console.Error.Flush();
        }

        private static void Process(SoundTouchProcessor processor, WaveFileReader inputFile, Stream? outputFile)
        {
            if (inputFile is null || outputFile is null)
                return;

            int channels = inputFile.WaveFormat.Channels;
            Debug.Assert(channels > 0, "input has at least one channel");

            using var inputStream = new WaveChannel32(inputFile) { PadWithZeroes = false };
            using var processStream = new SoundTouchWaveStream(inputStream, processor);
            using var outputStream = new Wave32To16Stream(processStream);

            WaveFileWriter.WriteWavFileToStream(outputFile, outputStream);
        }

        private static void DetectBpm(WaveFileReader inputFile, RunParameters parameters)
        {
            using var inputStream = new WaveChannel32(inputFile) { PadWithZeroes = false };

            int channels = inputFile.WaveFormat.Channels;
            var bpm = new BpmDetect(channels, inputFile.WaveFormat.SampleRate);

            Span<float> sampleBuffer = new float[BUFF_SIZE];

            while (inputStream.Position < inputStream.Length)
            {
                // Read sample data from input file
                var buffer = MemoryMarshal.AsBytes(sampleBuffer);
                int read = inputStream.Read(buffer);

                // Feed the samples into SoundTouch processor
                int sampleCount = read / (channels * sizeof(float));
                bpm.InputSamples(sampleBuffer, sampleCount);
            }

            var bpmValue = bpm.GetBpm();
            Console.Error.WriteLine("Done!");

            // Rewind the file after bpm detection.
            inputFile.Seek(0, SeekOrigin.Begin);

            if (bpmValue > 0)
            {
                Console.Error.WriteLine("Detected BPM rate {0:0.0}", bpmValue);
            }
            else
            {
                Console.Error.WriteLine("Couldn't detect BPM rate.");
            }

            if (parameters.GoalBpm > 0)
            {
                parameters.TempoDelta = ((parameters.GoalBpm / bpmValue) - 1.0) * 100.0;
                Console.Error.Write("The file will be converted to {0:0.0} BPM", parameters.GoalBpm);
            }
        }
    }
}
