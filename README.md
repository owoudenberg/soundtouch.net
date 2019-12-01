# soundtouch.net
The SoundTouch Library is originally written by Olli Parviainen in C++. Although a .NET wrapper library is available, this library aims to be a complete rewrite in C#.

## SoundTouch library Features

* Easy-to-use implementation of time-stretch, pitch-shift and sample rate transposing routines.
* Full source codes available for both the SoundTouch library and the example application.
* Clear and easy-to-use programming interface via a single C# class.
* Supported audio data format : 32bit floating point PCM mono/stereo
* Capable of real-time audio stream processing:
  * input/output latency max. ~ 100 ms.
  * Processing 44.1kHz/16bit stereo sound in realtime requires a 400 Mhz Intel Pentium processor or better.
* Released under the GNU Lesser General Public License (LGPL) v2.1

Original SoundTouch C++ project: http://www.surina.net/soundtouch

## Language and platforms

SoundTouch.NET is written in C# and targets .net standard, .net framework and .net core.
That makes it usable in most platforms:

* Windows (including UWP)
* Linux
* Max OS
* iOS
* Android