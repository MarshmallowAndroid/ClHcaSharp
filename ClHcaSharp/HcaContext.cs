namespace ClHcaSharp
{
    internal class HcaContext
    {
        public uint Version { get; set; }
        public uint HeaderSize { get; set; }

        public uint ChannelCount { get; set; }
        public uint SampleRate { get; set; }
        public uint FrameCount { get; set; }
        public uint EncoderDelay { get; set; }
        public uint EncoderPadding { get; set; }

        public uint FrameSize { get; set; }
        public uint MinResolution { get; set; }
        public uint MaxResolution { get; set; }
        public uint TrackCount { get; set; }
        public uint ChannelConfig { get; set; }
        public uint StereoType { get; set; }
        public uint TotalBandCount { get; set; }
        public uint BaseBandCount { get; set; }
        public uint StereoBandCount { get; set; }
        public uint BandsPerHfrGroup { get; set; }
        public uint MsStereo { get; set; }
        public uint Reserved { get; set; }

        public uint VbrMaxFrameSize { get; set; }
        public uint VbrNoiseLevel { get; set; }

        public uint AthType { get; set; }

        public uint LoopStartFrame { get; set; }
        public uint LoopEndFrame { get; set; }
        public uint LoopStartDelay { get; set; }
        public uint LoopEndPadding { get; set; }
        public uint LoopFlag { get; set; }

        public uint CiphType { get; set; }
        public ulong KeyCode { get; set; }

        public float RvaVolume { get; set; }

        public uint CommentLength { get; set; }
        public string Comment { get; set; }

        public uint HfrGroupCount { get; set; }
        public byte[] AthCurve { get; set; }
        public byte[] CipherTable { get; set; }

        public uint Random { get; set; }
        public Channel[] Channels { get; set; }
    }
}