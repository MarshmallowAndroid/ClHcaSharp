# ClHcaSharp

[vgmstream](https://github.com/vgmstream/vgmstream)'s clHCA decoder quickly written in C#.

I wanted to decode Umamusume (ウマ娘) audio files in C# but [VGAudio](https://github.com/Thealexbarney/VGAudio) has not been updated for HCA v3.0 so I did this instead.

The code is a quick and dirty port of the library, and as such does not guarantee speed. `unsafe` contexts were not used whatsoever.
