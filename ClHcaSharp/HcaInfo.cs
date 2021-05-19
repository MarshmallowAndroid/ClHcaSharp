namespace ClHcaSharp
{
    public class HcaInfo
    {
        public uint Version { get; set; }
        public uint HeaderSize { get; set; }
        public uint SamplingRate { get; set; }
        public uint ChannelCount { get; set; }
        public uint BlockSize { get; set; }
        public uint BlockCount { get; set; }
        public uint EncoderDelay { get; set; }
        public uint EncoderPadding { get; set; }
        public uint LoopEnabled { get; set; }
        public uint LoopStartBlock { get; set; }
        public uint LoopEndBlock { get; set; }
        public uint LoopStartDelay { get; set; }
        public uint LoopEndPadding { get; set; }
        public uint SamplesPerBlock { get; set; }
        public string Comment { get; set; }
        public uint EncryptionEnabled { get; set; }

        public uint SampleCount => BlockCount * SamplesPerBlock - EncoderDelay - EncoderPadding;
        public uint LoopStartSample => LoopStartBlock * SamplesPerBlock - EncoderDelay + LoopStartDelay;
        public uint LoopEndSample => LoopEndBlock * SamplesPerBlock - EncoderDelay + (SamplesPerBlock - LoopEndPadding);
    }
}