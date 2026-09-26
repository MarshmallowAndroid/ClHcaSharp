/**
 * Copyright (c) 2008-2025 Adam Gashlin, Fastelbja, Ronny Elfert, bnnm,
 *                         Christopher Snowhill, NicknineTheEagle, bxaimc,
 *                         Thealexbarney, CyberBotX, EdnessP, et al 
 * 
 * Portions of this code taken from vgmstream (https://github.com/vgmstream/vgmstream)
 * 
 * src/coding/hca_decoder.c
 * 
 */

using static ClHcaSharp.Constants;

namespace ClHcaSharp
{
    internal class Channel
    {
        public ChannelType Type { get; set; }
        public int CodedCount { get; set; }

        public byte[] Intensity { get; } = new byte[Subframes];
        public byte[] ScaleFactors { get; } = new byte[SamplesPerSubframe];
        public byte[] Resolution { get; } = new byte[SamplesPerSubframe];
        public byte[] Noises { get; } = new byte[SamplesPerSubframe];
        public int NoiseCount { get; set; }
        public int ValidCount { get; set; }

        public float[] Gain { get; } = new float[SamplesPerSubframe];
        public float[][] Spectra { get; } =
            JaggedArray.CreateJaggedArray<float[][]>(
                Subframes, SamplesPerSubframe);
        public float[] Temp { get; } = new float[SamplesPerSubframe];
        public float[] Dct { get; } = new float[SamplesPerSubframe];
        public float[] ImdctPrevious { get; } = new float[SamplesPerSubframe];

        public float[][] Wave { get; } =
            JaggedArray.CreateJaggedArray<float[][]>(
                Subframes, SamplesPerSubframe);
    }
}