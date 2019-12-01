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
    using System.Globalization;
    using System.Text;

    public sealed class RunParameters
    {
        private const string LICENSE_TEXT = @"    LICENSE:
    ========
        
    SoundTouch sound processing library
    Copyright (c) Olli Parviainen
        
    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License version 2.1 as published by the Free Software Foundation.
        
    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
        
This application is distributed with full source codes; however, if you
didn't receive them, please visit the author's homepage (see the link above).";

        private const string WHAT_TEXT = @"This application processes WAV audio files by modifying the sound tempo,
pitch and playback rate properties independently from each other.

";

        private const string USAGE = @"Usage :
    soundstretch infilename outfilename [switches]

To use standard input/output pipes, give 'stdin' and 'stdout' as filenames.

Available switches are:
  -tempo=n : Change sound tempo by n percents  (n=-95..+5000 %)
  -pitch=n : Change sound pitch by n semitones (n=-60..+60 semitones)
  -rate=n  : Change sound rate by n percents   (n=-95..+5000 %)
  -bpm=n   : Detect the BPM rate of sound and adjust tempo to meet 'n' BPMs.
             If '=n' is omitted, just detects the BPM rate.
  -quick   : Use quicker tempo change algorithm (gain speed, lose quality)
  -naa     : Don't use anti-alias filtering (gain speed, lose quality)
  -speech  : Tune algorithm for speech processing (default is for music)
  -license : Display the program license text (LGPL)";

        public RunParameters(in Span<string> args)
        {
            int firstParam;

            if (args.Length < 3)
            {
                // Too few parameters
                if (args.Length > 0 && string.Equals(args[0], "-l", StringComparison.OrdinalIgnoreCase))
                    throw new LicenseException();

                throw new InvalidOperationException(WHAT_TEXT + USAGE);
            }

            InFileName = args[0];
            OutFileName = args[1];

            if (OutFileName[0] == '-')
            {
                OutFileName = null;
                firstParam = 1;
            }
            else
            {
                firstParam = 2;
            }

            for (int i = firstParam; i < args.Length; ++i)
            {
                ParseSwitchArg(args[i]);
            }

            CheckLimits();
        }

        public string InFileName { get; }

        public string? OutFileName { get; }

        public double TempoDelta { get; set; }

        public double PitchDelta { get; private set; }

        public double RateDelta { get; private set; }

        public bool Quick { get; private set; }

        public bool NoAntiAlias { get; private set; }

        public double GoalBpm { get; private set; }

        public bool DetectBpm { get; private set; }

        public bool Speech { get; private set; }

        private static double ParseSwitchValue(in ReadOnlySpan<char> str)
        {
            int pos = str.IndexOf('=');
            if (pos >= 0 && double.TryParse(str.Slice(pos + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;

            throw new IllegalArgumentsException(str);
        }

        private void ParseSwitchArg(in ReadOnlySpan<char> str)
        {
            if (str[0] != '-')
                throw new IllegalArgumentsException(str);

            switch (char.ToLowerInvariant(str[1]))
            {
                case 't':
                    TempoDelta = ParseSwitchValue(str);
                    break;

                case 'p':
                    PitchDelta = ParseSwitchValue(str);
                    break;

                case 'r':
                    RateDelta = ParseSwitchValue(str);
                    break;

                case 'b':
                    DetectBpm = true;
                    try
                    {
                        GoalBpm = ParseSwitchValue(str);
                    }
                    catch (IllegalArgumentsException)
                    {
                        GoalBpm = 0;
                    }

                    break;

                case 'q':
                    Quick = true;
                    break;

                case 'n':
                    NoAntiAlias = true;
                    break;

                case 'l':
                    throw new LicenseException();

                case 's':
                    Speech = true;
                    break;

                default:
                    throw new IllegalArgumentsException(str);
            }
        }

        private void CheckLimits()
        {
            if (TempoDelta < -95)
                TempoDelta = -95;
            else if (TempoDelta > 5000)
                TempoDelta = 5000;

            if (PitchDelta < -60)
                PitchDelta = -60;
            else if (PitchDelta > 60)
                PitchDelta = 60;

            if (RateDelta < -95)
                RateDelta = -95;
            else if (RateDelta > 5000)
                RateDelta = 5000;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S3871:Exception types should be \"public\"", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194:Implement exception constructors.", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Reviewed.")]
        private class IllegalArgumentsException : Exception
        {
            public IllegalArgumentsException(in ReadOnlySpan<char> str)
                : base(FormatMessage(str))
            {
            }

            private static string FormatMessage(in ReadOnlySpan<char> message)
            {
                var build = new StringBuilder("ERROR: Illegal parameter \"")
                .Append(message)
                .AppendLine("\".")
                .AppendLine()
                .Append(USAGE);

                return build.ToString();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S3871:Exception types should be \"public\"", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194:Implement exception constructors.", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Reviewed.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Reviewed.")]
        private class LicenseException : Exception
        {
            public LicenseException()
                : base(LICENSE_TEXT)
            {
            }
        }
    }
}
