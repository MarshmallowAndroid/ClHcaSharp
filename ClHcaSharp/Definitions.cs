namespace ClHcaSharp
{
    public static class Constants
    {
        public const uint Version101 = 0x0101;
        public const uint Version102 = 0x0102;
        public const uint Version103 = 0x0103;
        public const uint Version200 = 0x0200;
        public const uint Version300 = 0x0300;

        public const uint MinFrameSize = 0x8;
        public const uint MaxFrameSize = 0xFFFF;

        public const uint Mask = 0x7F7F7F7F;
        public const uint SubframesPerFrame = 8;
        public const uint SamplesPerSubframe = 128;
        public const uint SamplesPerFrame = SubframesPerFrame * SamplesPerSubframe;
        public const uint MdctBits = 7;

        public const uint MinChannels = 1;
        public const uint MaxChannels = 16;
        public const uint MinSampleRate = 1;
        public const uint MaxSampleRate = 0x7FFFFF;

        public const uint DefaultRandom = 1;
    }

    public enum ChannelType
    {
        Discrete = 0,
        StereoPrimary = 1,
        StereoSecondary = 2
    }
}