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

namespace SoundStretch
{
    public class RunParameters
    {
        private const string LICENSETEXT =
            "    LICENSE:\n" +
            "    ========\n" +
            "    \n" +
            "    SoundTouch sound processing library\n" +
            "    Copyright (c) Olli Parviainen\n" +
            "     C# port Copyright (c) Olaf Woudenberg\n" +
            "    \n" +
            "    This library is free software; you can redistribute it and/or\n" +
            "    modify it under the terms of the GNU Lesser General Public\n" +
            "    License version 2.1 as published by the Free Software Foundation.\n" +
            "    \n" +
            "    This library is distributed in the hope that it will be useful,\n" +
            "    but WITHOUT ANY WARRANTY; without even the implied warranty of\n" +
            "    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU\n" +
            "    Lesser General Public License for more details.\n" +
            "    \n" +
            "    You should have received a copy of the GNU Lesser General Public\n" +
            "    License along with this library; if not, write to the Free Software\n" +
            "    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA\n" +
            "    \n" +
            "This application is distributed with full source codes; however, if you\n" +
            "didn't receive them, please visit the author's homepage (see the link above).";

        private const string WHATTEXT =
            "This application processes WAV audio files by modifying the sound tempo,\n" +
            "pitch and playback rate properties independently from each other.\n" +
            "\n";

        private const string USAGE =
            "Usage :\n" +
            "    soundstretch infilename outfilename [switches]\n" +
            "\n" +
            "To use standard input/output pipes, give 'stdin' and 'stdout' as filenames.\n" +
            "\n" +
            "Available switches are:\n" +
            "  -tempo=n : Change sound tempo by n percents  (n=-95..+5000 %)\n" +
            "  -pitch=n : Change sound pitch by n semitones (n=-60..+60 semitones)\n" +
            "  -rate=n  : Change sound rate by n percents   (n=-95..+5000 %)\n" +
            "  -bpm=n   : Detect the BPM rate of sound and adjust tempo to meet 'n' BPMs.\n" +
            "             If '=n' is omitted, just detects the BPM rate.\n" +
            "  -quick   : Use quicker tempo change algorithm (gain speed, lose quality)\n" +
            "  -naa     : Don't use anti-alias filtering (gain speed, lose quality)\n" +
            "  -speech  : Tune algorithm for speech processing (default is for music)\n" +
            "  -license : Display the program license text (LGPL)\n";

        /// <exception cref="Exception">Illegal parameter.</exception>
        private static void ThrowIllegalParamExp(string str)
        {
            string msg = string.Format("ERROR : Illegal parameter \"{0}\".\n\n", str);
            msg += USAGE;
            throw new Exception(msg);
        }

        /// <exception cref="Exception">License information.</exception>
        private static void ThrowLicense()
        {
            throw new Exception(LICENSETEXT);
        }

        private static float ParseSwitchValue(string str)
        {
            int pos = str.IndexOf('=');
            if (pos < 0)
            {
                // '=' missing
                ThrowIllegalParamExp(str);
            }

            // Read numerical parameter value after '='
            return float.Parse(str.Substring(pos + 1));
        }
        
        private void CheckLimits()
        {
            if (TempoDelta < -95.0f)
            {
                TempoDelta = -95.0f;
            }
            else if (TempoDelta > 5000.0f)
            {
                TempoDelta = 5000.0f;
            }

            if (PitchDelta < -60.0f)
            {
                PitchDelta = -60.0f;
            }
            else if (PitchDelta > 60.0f)
            {
                PitchDelta = 60.0f;
            }

            if (RateDelta < -95.0f)
            {
                RateDelta = -95.0f;
            }
            else if (RateDelta > 5000.0f)
            {
                RateDelta = 5000.0f;
            }
        }

        private void ParseSwitchParam(string str)
        {
            if (str[0] != '-')
            {
                // leading hyphen missing => not a valid parameter
                ThrowIllegalParamExp(str);
            }

            // Take the first character of switch name & change to lower case
            int upS = char.ToLower(str[1]);

            // interpret the switch name & operate accordingly
            switch (upS)
            {
                case 't':
                    // switch '-tempo=xx'
                    TempoDelta = ParseSwitchValue(str);
                    break;

                case 'p':
                    // switch '-pitch=xx'
                    PitchDelta = ParseSwitchValue(str);
                    break;

                case 'r':
                    // switch '-rate=xx'
                    RateDelta = ParseSwitchValue(str);
                    break;

                case 'b':
                    // switch '-bpm=xx'
                    DetectBpm = true;
                    try
                    {
                        GoalBpm = ParseSwitchValue(str);
                    }
                    catch (Exception)
                    {
                        // illegal or missing bpm value => just calculate bpm
                        GoalBpm = 0;
                    }
                    break;

                case 'q':
                    // switch '-quick'
                    Quick = 1;
                    break;

                case 'n':
                    // switch '-naa'
                    NoAntiAlias = 1;
                    break;

                case 'l':
                    // switch '-license'
                    ThrowLicense();
                    break;

                case 's':
                    // switch '-speech'
                    Speech = true;
                    break;

                default:
                    // unknown switch
                    ThrowIllegalParamExp(str);
                    break;
            }
        }

        public readonly string InFileName;
        public readonly string OutFileName;
        public float TempoDelta;
        public float PitchDelta;
        public float RateDelta;
        public int Quick;
        public int NoAntiAlias;
        public float GoalBpm;
        public bool DetectBpm;
        public bool Speech;

        /// <exception cref="Exception">WHATTEXT + USAGE - or - LICENSE - or - Illegal parameter.</exception>
        public RunParameters(string[] args)
        {
            int i;
            int nFirstParam;

            if (args.Length < 2)
            {
                // Too few parameters
                if (args.Length > 0 && args[0][0] == '-' && char.ToLower(args[0][1]) == 'l')
                {
                    // '-license' switch
                    ThrowLicense();
                }
                string msg = WHATTEXT;
                msg += USAGE;
                throw new Exception(msg);
            }

            InFileName = null;
            OutFileName = null;
            TempoDelta = 0;
            PitchDelta = 0;
            RateDelta = 0;
            Quick = 0;
            NoAntiAlias = 0;
            GoalBpm = 0;
            Speech = false;
            DetectBpm = false;

            // Get input & output file names
            InFileName = args[0];
            OutFileName = args[1];

            if (OutFileName[0] == '-')
            {
                // no outputfile name was given but parameters
                OutFileName = null;
                nFirstParam = 1;
            }
            else
            {
                nFirstParam = 2;
            }

            // parse switch parameters
            for (i = nFirstParam; i < args.Length; i++)
            {
                ParseSwitchParam(args[i]);
            }

            CheckLimits();
        }
    }
}