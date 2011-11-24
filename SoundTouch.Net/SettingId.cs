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

namespace SoundTouch
{
    /// <summary>
    /// Available setting IDs for the 
    /// <see cref="SoundTouch{TSampletype,TLongSampleType}.SetSetting"/> & 
    /// <see cref="SoundTouch{TSampletype,TLongSampleType}.GetSetting"/>
    /// functions.
    /// </summary>
    public enum SettingId
    {
        /// <summary>Enable/disable anti-alias filter in pitch transposer (0 =
        /// disable)</summary>
        UseAntiAliasFilter = 0,

        /// <summary> Pitch transposer anti-alias filter length (8 .. 128 taps,
        /// default = 32)</summary>
        AntiAliasFilterLength = 1,

        /// <summary> Enable/disable quick seeking algorithm in tempo changer
        /// routine (enabling quick seeking lowers CPU utilization but causes a
        /// minor sound quality compromising)</summary>
        UseQuickseek = 2,

        /// <summary> Time-stretch algorithm single processing sequence length
        /// in milliseconds. This determines  to how long sequences the original
        /// sound is chopped in the time-stretch algorithm.  See "STTypes.h" or
        /// README for more information.</summary>
        SequenceDurationMs = 3,

        /// <summary> Time-stretch algorithm seeking window length in
        /// milliseconds for algorithm that finds the  best possible overlapping
        /// location. This determines from how wide window the algorithm  may
        /// look for an optimal joining location when mixing the sound sequences
        /// back together.  See README for more information.</summary>
        SeekwindowDurationMs = 4,

        /// <summary> Time-stretch algorithm overlap length in milliseconds.
        /// When the chopped sound sequences  are mixed back together, to form a
        /// continuous sound stream, this parameter defines over  how long
        /// period the two consecutive sequences are let to overlap each other. 
        /// See README for more information.</summary>
        OverlapDurationMs = 5,

        /// <summary> Call "getSetting" with this ID to query nominal average
        /// processing sequence size in samples. This value tells approcimate
        /// value how many input samples  SoundTouch needs to gather before it
        /// does DSP processing run for the sample batch.
        ///</summary>
        /// <remarks>
        /// This is read-only parameter, i.e. setSetting ignores this
        /// parameter - Returned value is approximate average value, exact
        /// processing batch size may wary from time to time - This parameter
        /// value is not constant but may change depending on 
        /// tempo/pitch/rate/samplerate settings.</remarks>
        NominalInputSequence = 6,


        /// <summary> Call "getSetting" with this ID to query nominal average
        /// processing output  size in samples. This value tells approcimate
        /// value how many output samples  SoundTouch outputs once it does DSP
        /// processing run for a batch of input samples.</summary>
        ///	<remarks>
        /// This is read-only parameter, i.e. SetSetting ignores this
        /// parameter - Returned value is approximate average value, exact
        /// processing batch size may wary from time to time - This parameter
        /// value is not constant but may change depending on 
        /// tempo/pitch/rate/samplerate settings.</remarks>
        NominalOutputSequence = 7,
    }
}