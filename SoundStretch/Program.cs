//#define SOUNDTOUCH_INTEGER_SAMPLES
#define SOUNDTOUCH_FLOAT_SAMPLES

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
using System.Diagnostics;
using SoundTouch;

#if SOUNDTOUCH_INTEGER_SAMPLES
using TSampleType = System.Int16;
using TLongSampleType = System.Int64;
#else
using TSampleType = System.Single;
using TLongSampleType = System.Double;
#endif

namespace SoundStretch
{
    internal static class Program
    {
        // Processing chunk size
        private const int BUFF_SIZE = 2048;

        private static readonly string[] HELLOTEXT = new[]
        {
            Environment.NewLine,
            "   SoundStretch v{0} - C++ Version Written by Olli Parviainen 2001 - 2012",
            Environment.NewLine,
            "   SoundStretch v{0} - C# Version Written by Olaf Woudenberg 2011 - 2013",
            Environment.NewLine,
            "=============================================================================",
            Environment.NewLine,
            "author e-mail: <o.woudenberg",
            "@",
            "mijnbytes.nl>",
            Environment.NewLine,
            "          WWW: http://www.surina.net/soundtouch\n",
            Environment.NewLine,
            "This program is subject to (L)GPL license. Run \"SoundStretch -license\" for",
            Environment.NewLine,
            "more information.",
            Environment.NewLine,
        };


        private static void OpenFiles(out WavInFile inFile, out WavOutFile outFile, RunParameters parameters)
        {
            inFile = parameters.InFileName == "stdin" ? new WavInFile(Console.OpenStandardInput()) : new WavInFile(parameters.InFileName);

            // ... open output file with same sound parameters
            int bits = (inFile).GetNumBits();
            int samplerate = (inFile).GetSampleRate();
            int channels = (inFile).GetNumChannels();

            if (parameters.OutFileName != null)
            {
                outFile = parameters.OutFileName == "stdout" ? new WavOutFile(Console.OpenStandardOutput(), samplerate, bits, channels) : new WavOutFile(parameters.OutFileName, samplerate, bits, channels);
            }
            else
            {
                outFile = null;
            }
        }

        /// <summary>
        /// Sets the <c>SoundTouch</c> object up according to input file sound format & command line parameters
        /// </summary>
        private static void Setup(SoundTouch<TSampleType, TLongSampleType> pSoundTouch, WavInFile inFile, RunParameters parameters)
        {
            int sampleRate = inFile.GetSampleRate();
            int channels = inFile.GetNumChannels();
            pSoundTouch.SetSampleRate(sampleRate);
            pSoundTouch.SetChannels(channels);

            pSoundTouch.SetTempoChange(parameters.TempoDelta);
            pSoundTouch.SetPitchSemiTones(parameters.PitchDelta);
            pSoundTouch.SetRateChange(parameters.RateDelta);

            pSoundTouch.SetSetting(SettingId.UseQuickseek, parameters.Quick);
            pSoundTouch.SetSetting(SettingId.UseAntiAliasFilter, (parameters.NoAntiAlias == 1) ? 0 : 1);

            if (parameters.Speech)
            {
                // use settings for speech processing
                pSoundTouch.SetSetting(SettingId.SequenceDurationMs, 40);
                pSoundTouch.SetSetting(SettingId.SeekwindowDurationMs, 15);
                pSoundTouch.SetSetting(SettingId.OverlapDurationMs, 8);
                Console.Error.WriteLine("Tune processing parameters for speech processing.");
            }

            // print processing information
            if (parameters.OutFileName != null)
            {
#if SOUNDTOUCH_INTEGER_SAMPLES
                Console.Error.WriteLine("Uses 16bit integer sample type in processing.\n");
#else
#if !SOUNDTOUCH_FLOAT_SAMPLES
    #error "Sampletype not defined"
#endif
                Console.Error.WriteLine("Uses 32bit floating point sample type in processing.\n");
#endif
                // print processing information only if outFileName given i.e. some processing will happen
                Console.Error.WriteLine("Processing the file with the following changes:");
                Console.Error.WriteLine("  tempo change = {0:0.00} %", parameters.TempoDelta);
                Console.Error.WriteLine("  pitch change = {0} semitones", parameters.PitchDelta);
                Console.Error.WriteLine("  rate change  = {0:0.00} %\n", parameters.RateDelta);
                Console.Error.Write("Working...");
            }
            else
            {
                // outFileName not given
                Console.Error.WriteLine("Warning: output file name missing, won't output anything.\n");
            }
            Console.Error.Flush();
        }


        /// <summary>
        /// Processes the sound.
        /// </summary>
        private static void Process(SoundTouch<TSampleType, TLongSampleType> pSoundTouch, WavInFile inFile, WavOutFile outFile)
        {
            int nSamples;
            var sampleBuffer = new TSampleType[BUFF_SIZE];

            if ((inFile == null) || (outFile == null)) return; // nothing to do.

            int nChannels = inFile.GetNumChannels();
            Debug.Assert(nChannels > 0);
            int buffSizeSamples = BUFF_SIZE/nChannels;

            // Process samples read from the input file
            while (!inFile.Eof())
            {
                // Read a chunk of samples from the input file
                int num = inFile.Read(sampleBuffer, BUFF_SIZE);
                nSamples = num/inFile.GetNumChannels();

                // Feed the samples into SoundTouch processor
                pSoundTouch.PutSamples(sampleBuffer, nSamples);

                // Read ready samples from SoundTouch processor & write them output file.
                // NOTES:
                // - 'receiveSamples' doesn't necessarily return any samples at all
                //   during some rounds!
                // - On the other hand, during some round 'receiveSamples' may have more
                //   ready samples than would fit into 'sampleBuffer', and for this reason 
                //   the 'receiveSamples' call is iterated for as many times as it
                //   outputs samples.
                do
                {
                    nSamples = pSoundTouch.ReceiveSamples(sampleBuffer, buffSizeSamples);
                    outFile.Write(sampleBuffer, nSamples*nChannels);
                } while (nSamples != 0);
            }

            // Now the input file is processed, yet 'flush' few last samples that are
            // hiding in the SoundTouch's internal processing pipeline.
            pSoundTouch.Flush();
            do
            {
                nSamples = pSoundTouch.ReceiveSamples(sampleBuffer, buffSizeSamples);
                outFile.Write(sampleBuffer, nSamples*nChannels);
            } while (nSamples != 0);
        }

        /// <summary>
        /// Detect BPM rate of <paramref name="inFile"/> and adjust tempo
        /// setting accordingly if necessary.
        /// </summary>
        private static void DetectBpm(WavInFile inFile, RunParameters parameters)
        {
            var bpm = BpmDetect<TSampleType, TLongSampleType>.NewInstance(inFile.GetNumChannels(), inFile.GetSampleRate());
            var sampleBuffer = new TSampleType[BUFF_SIZE];

            // detect bpm rate
            Console.Error.Write("Detecting BPM rate...");
            Console.Error.Flush();

            int nChannels = inFile.GetNumChannels();
            Debug.Assert(BUFF_SIZE%nChannels == 0);

            // Process the 'inFile' in small blocks, repeat until whole file has 
            // been processed
            while (inFile.Eof() == false)
            {
                // Read sample data from input file
                int num = inFile.Read(sampleBuffer, BUFF_SIZE);

                // Enter the new samples to the bpm analyzer class
                int samples = num/nChannels;
                bpm.InputSamples(sampleBuffer, samples);
            }

            // Now the whole song data has been analyzed. Read the resulting bpm.
            float bpmValue = bpm.GetBpm();
            Console.Error.WriteLine("Done!");

            // rewind the file after bpm detection
            inFile.Rewind();

            if (bpmValue > 0)
            {
                Console.Error.WriteLine("Detected BPM rate {0:0.0}\n", bpmValue);
            }
            else
            {
                Console.Error.WriteLine("Couldn't detect BPM rate.\n");
                return;
            }

            if (parameters.GoalBpm > 0)
            {
                // adjust tempo to given bpm
                parameters.TempoDelta = (parameters.GoalBpm/bpmValue - 1.0f)*100.0f;
                Console.Error.WriteLine("The file will be converted to {0:0.0} BPM\n", parameters.GoalBpm);
            }
        }

        private static int Main(string[] args)
        {
            WavInFile inFile;
            WavOutFile outFile;
            var soundTouch = new SoundTouch<TSampleType, TLongSampleType>();

            Console.Error.WriteLine(string.Format(string.Concat(HELLOTEXT), SoundTouch<TSampleType, TLongSampleType>.VersionString));

            try
            {
                // Parse command line parameters
                var parameters = new RunParameters(args);

                // Open input & output files
                OpenFiles(out inFile, out outFile, parameters);

                if (parameters.DetectBpm)
                {
                    // detect sound BPM (and adjust processing parameters
                    //  accordingly if necessary)
                    DetectBpm(inFile, parameters);
                }

                // Setup the 'SoundTouch' object for processing the sound
                Setup(soundTouch, inFile, parameters);

                // Process the sound
                Process(soundTouch, inFile, outFile);

                if (inFile != null) inFile.Dispose();
                if (outFile != null) outFile.Dispose();

                Console.Error.WriteLine("Done!");
            }
            catch (Exception e)
            {
                // An exception occurred during processing, display an error message
                Console.Error.WriteLine(e.Message);
                return -1;
            }

            return 0;
        }
    }
}