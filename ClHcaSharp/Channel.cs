using static ClHcaSharp.Constants;

namespace ClHcaSharp
{
    public class Channel
    {
        public ChannelType Type { get; set; }
        public uint CodedCount { get; set; }

        public byte[] Intensity { get; } = new byte[SubframesPerFrame];
        public byte[] ScaleFactors { get; } = new byte[SamplesPerSubframe];
        public byte[] Resolution { get; } = new byte[SamplesPerSubframe];
        public byte[] Noises { get; } = new byte[SamplesPerSubframe];
        public uint NoiseCount { get; set; }
        public uint ValidCount { get; set; }

        public float[] Gain { get; } = new float[SamplesPerSubframe];
        public float[] Spectra { get; } = new float[SamplesPerSubframe];
        public float[] Temp { get; } = new float[SamplesPerSubframe];
        public float[] Dct { get; } = new float[SamplesPerSubframe];
        public float[] ImdctPrevious { get; } = new float[SamplesPerSubframe];

        public float[][] Wave { get; } =
            JaggedArray.CreateJaggedArray<float[][]>(
                (int)SubframesPerFrame, (int)SamplesPerSubframe);
    }
}