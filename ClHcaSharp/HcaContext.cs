/**
 * Copyright (c) 2008-2025 Adam Gashlin, Fastelbja, Ronny Elfert, bnnm,
 *                         Christopher Snowhill, NicknineTheEagle, bxaimc,
 *                         Thealexbarney, CyberBotX, EdnessP, et al 
 * 
 * Portions of this code taken from vgmstream (https://github.com/vgmstream/vgmstream)
 * 
 * src/coding/libs/clhca.c
 * 
 */

using System.IO;
using System.Text;
using static ClHcaSharp.Constants;

namespace ClHcaSharp
{
    internal class HcaContext
    {
        public HcaContext(Stream hcaStream)
        {
            if (!IsHeaderValid(hcaStream, out int headerSize))
                throw new HcaHeaderException();

            hcaStream.Position = 0;

            byte[] headerBytes = new byte[headerSize];
            hcaStream.Read(headerBytes, 0, headerSize);

            BitReader bitReader = new BitReader(headerBytes);

            if ((bitReader.Peek(32) & Mask) == StringToUInt32("HCA"))
            {
                bitReader.Skip(32);
                Version = bitReader.Read(16);
                HeaderSize = bitReader.Read(16);

                if (Version != Version101 &&
                    Version != Version102 &&
                    Version != Version103 &&
                    Version != Version200 &&
                    Version != Version300)
                    throw new HcaHeaderException();

                if (headerSize < HeaderSize)
                    throw new HcaParamsException();

                if (Crc.Crc16Checksum(headerBytes) > 0)
                    throw new HcaChecksumException();

                headerSize -= 8;
            }
            else throw new HcaHeaderException();

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("fmt"))
            {
                bitReader.Skip(32);
                ChannelCount = bitReader.Read(8);
                SampleRate = bitReader.Read(24);
                FrameCount = bitReader.Read(32);
                EncoderDelay = bitReader.Read(16);
                EncoderPadding = bitReader.Read(16);

                if (!(ChannelCount >= MinChannels && ChannelCount <= MaxChannels))
                    throw new HcaHeaderException();

                if (FrameCount == 0)
                    throw new HcaHeaderException();

                if (!(SampleRate >= MinSampleRate && SampleRate <= MaxSampleRate))
                    throw new HcaHeaderException();

                headerSize -= 16;
            }
            else throw new HcaHeaderException();

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("comp"))
            {
                bitReader.Skip(32);
                FrameSize = bitReader.Read(16);
                MinResolution = bitReader.Read(8);
                MaxResolution = bitReader.Read(8);
                TrackCount = bitReader.Read(8);
                ChannelConfig = bitReader.Read(8);
                TotalBandCount = bitReader.Read(8);
                BaseBandCount = bitReader.Read(8);
                StereoBandCount = bitReader.Read(8);
                BandsPerHfrGroup = bitReader.Read(8);
                MsStereo = bitReader.Read(8);
                Reserved = bitReader.Read(8);

                headerSize -= 16;
            }
            else if (headerSize >= 12 && (bitReader.Peek(32) & Mask) == StringToUInt32("dec"))
            {
                bitReader.Skip(32);
                FrameSize = bitReader.Read(16);
                MinResolution = bitReader.Read(8);
                MaxResolution = bitReader.Read(8);
                TotalBandCount = bitReader.Read(8) + 1;
                BaseBandCount = bitReader.Read(8) + 1;
                TrackCount = bitReader.Read(4);
                ChannelConfig = bitReader.Read(4);
                StereoType = bitReader.Read(8);

                if (StereoType == 0) BaseBandCount = TotalBandCount;
                StereoBandCount = TotalBandCount - BaseBandCount;
                BandsPerHfrGroup = 0;

                headerSize -= 12;
            }
            else throw new HcaHeaderException();

            if (headerSize >= 8 && (bitReader.Peek(32) & Mask) == StringToUInt32("vbr"))
            {
                bitReader.Skip(32);
                VbrMaxFrameSize = bitReader.Read(16);
                VbrNoiseLevel = bitReader.Read(16);

                if (!(FrameSize == 0 && VbrMaxFrameSize > 8 && VbrMaxFrameSize <= 511))
                    throw new HcaHeaderException();

                headerSize -= 8;
            }
            else
            {
                VbrMaxFrameSize = 0;
                VbrNoiseLevel = 0;
            }

            if (headerSize >= 6 && (bitReader.Peek(32) & Mask) == StringToUInt32("ath"))
            {
                bitReader.Skip(32);
                AthType = bitReader.Read(16);
            }
            else AthType = (Version < Version200) ? 1 : 0;

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("loop"))
            {
                bitReader.Skip(32);
                LoopStartFrame = bitReader.Read(32);
                LoopEndFrame = bitReader.Read(32);
                LoopStartDelay = bitReader.Read(16);
                LoopEndPadding = bitReader.Read(16);

                LoopFlag = true;

                if (!(LoopStartFrame >= 0 && LoopStartFrame <= LoopEndFrame
                    && LoopEndFrame < FrameCount))
                    throw new HcaHeaderException();

                headerSize -= 16;
            }
            else
            {
                LoopStartFrame = 0;
                LoopEndFrame = 0;
                LoopStartDelay = 0;
                LoopEndPadding = 0;
                LoopFlag = false;
            }

            if (headerSize >= 6 && (bitReader.Peek(32) & Mask) == StringToUInt32("ciph"))
            {
                bitReader.Skip(32);
                CiphType = bitReader.Read(16);

                if (!(CiphType == 0 || CiphType == 1 || CiphType == 56))
                    throw new HcaHeaderException();
                headerSize -= 6;
            }

            if (headerSize >= 8 && (bitReader.Peek(32) & Mask) == StringToUInt32("rva"))
            {
                bitReader.Skip(32);
                int rvaVolumeInt = bitReader.Read(32);
                RvaVolume = Util.UInt32ToSingle((uint)rvaVolumeInt);

                headerSize -= 8;
            }
            else RvaVolume = 1.0F;

            if (headerSize >= 5 && (bitReader.Peek(32) & Mask) == StringToUInt32("comm"))
            {
                bitReader.Skip(32);
                CommentLength = bitReader.Read(8);

                if (CommentLength > headerSize) throw new HcaHeaderException();

                StringBuilder commentStringBuilder = new StringBuilder();
                for (int i = 0; i < CommentLength; i++)
                {
                    commentStringBuilder.Append(bitReader.Read(8));
                }

                Comment = commentStringBuilder.ToString();

                headerSize -= 5 + CommentLength;
            }
            else CommentLength = 0;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
            if (headerSize >= 4 && (bitReader.Peek(32) & Mask) == StringToUInt32("pad"))
            {
                headerSize -= (headerSize - 2);
            }
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            if (!(FrameSize >= MinFrameSize && FrameSize <= MaxFrameSize))
                throw new HcaHeaderException();

            if (Version <= Version200)
            {
                if (MinResolution != 1 || MaxResolution != 15)
                    throw new HcaHeaderException();
            }
            else if (MinResolution > MaxResolution || MaxResolution > 15)
                throw new HcaHeaderException();

            if (TrackCount == 0) TrackCount = 1;

            if (TrackCount > ChannelCount) throw new HcaHeaderException();

            if (TotalBandCount > SamplesPerSubframe || TotalBandCount == 0 ||
                BaseBandCount + StereoBandCount > TotalBandCount ||
                BaseBandCount + StereoBandCount == 0 ||
                StereoBandCount > BaseBandCount ||
                BandsPerHfrGroup > SamplesPerSubframe)
                throw new HcaHeaderException();

            HfrGroupCount = HeaderCeil2(
                TotalBandCount - BaseBandCount - StereoBandCount,
                BandsPerHfrGroup);

            ChannelType[] channelTypes = new ChannelType[MaxChannels];
            SetupChannelTypes(ChannelCount, TrackCount, ChannelConfig, StereoBandCount, channelTypes);
            
            for (int i = 0; i < ChannelCount; i++)
            {
                int channelStereoBandCount;

                if (channelTypes[i] == ChannelType.StereoSecondary)
                    channelStereoBandCount = 0;
                else
                    channelStereoBandCount = StereoBandCount;

                Channels[i] = new Channel
                {
                    CodedCount = BaseBandCount + channelStereoBandCount,
                    Type = channelTypes[i]
                };
            }

            Random = DefaultRandom;

            AthCurve = Ath.Init(AthType, SampleRate);
            CipherTable = Cipher.Init(CiphType, KeyCode);

            if (MsStereo > 0) throw new HcaHeaderException();
        }

        public int Version { get; set; }
        public int HeaderSize { get; set; }

        public int ChannelCount { get; set; }
        public int SampleRate { get; set; }
        public int FrameCount { get; set; }
        public int EncoderDelay { get; set; }
        public int EncoderPadding { get; set; }

        public int FrameSize { get; set; }
        public int MinResolution { get; set; }
        public int MaxResolution { get; set; }
        public int TrackCount { get; set; }
        public int ChannelConfig { get; set; }
        public int StereoType { get; set; }
        public int TotalBandCount { get; set; }
        public int BaseBandCount { get; set; }
        public int StereoBandCount { get; set; }
        public int BandsPerHfrGroup { get; set; }
        public int MsStereo { get; set; }
        public int Reserved { get; set; }

        public int VbrMaxFrameSize { get; set; }
        public int VbrNoiseLevel { get; set; }

        public int AthType { get; set; }

        public int LoopStartFrame { get; set; }
        public int LoopEndFrame { get; set; }
        public int LoopStartDelay { get; set; }
        public int LoopEndPadding { get; set; }
        public bool LoopFlag { get; set; }

        public int CiphType { get; set; }
        public ulong KeyCode { get; set; }

        public float RvaVolume { get; set; }

        public int CommentLength { get; set; }
        public string Comment { get; set; } = "";

        public int HfrGroupCount { get; set; }
        public byte[] AthCurve { get; set; }
        public byte[] CipherTable { get; set; }

        public uint Random { get; set; }
        public Channel[] Channels { get; set; } = new Channel[MaxChannels];

        public void SetKey(ulong key)
        {
            KeyCode = key;
            CipherTable = Cipher.Init(CiphType, KeyCode);
        }

        private static void SetupChannelTypes(int channels, int trackCount, int channelConfig, int stereoBandCount, ChannelType[] channelTypes)
        {
            int channelsPerTrack = channels / trackCount;
            if (stereoBandCount > 0 && channelsPerTrack > 1)
            {
                int channelTypesOffset = 0;
                for (int i = 0; i < trackCount; i++)
                {
                    switch (channelsPerTrack)
                    {
                        case 2:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            break;
                        case 3:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 2] = ChannelType.Discrete;
                            break;
                        case 4:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            if (channelConfig == 0)
                            {
                                channelTypes[channelTypesOffset + 2] = ChannelType.StereoPrimary;
                                channelTypes[channelTypesOffset + 3] = ChannelType.StereoSecondary;
                            }
                            else
                            {
                                channelTypes[channelTypesOffset + 2] = ChannelType.Discrete;
                                channelTypes[channelTypesOffset + 3] = ChannelType.Discrete;
                            }
                            break;
                        case 5:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            if (channelConfig <= 2)
                            {
                                channelTypes[channelTypesOffset + 3] = ChannelType.StereoPrimary;
                                channelTypes[channelTypesOffset + 4] = ChannelType.StereoSecondary;
                            }
                            else
                            {
                                channelTypes[channelTypesOffset + 3] = ChannelType.Discrete;
                                channelTypes[channelTypesOffset + 4] = ChannelType.Discrete;
                            }
                            break;

                        case 6:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 2] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 3] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 4] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 5] = ChannelType.StereoSecondary;
                            break;
                        case 7:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 2] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 3] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 4] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 5] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 6] = ChannelType.Discrete;
                            break;

                        case 8:
                            channelTypes[channelTypesOffset + 0] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 1] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 2] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 3] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 4] = ChannelType.StereoPrimary;
                            channelTypes[channelTypesOffset + 5] = ChannelType.StereoSecondary;
                            channelTypes[channelTypesOffset + 6] = ChannelType.Discrete;
                            channelTypes[channelTypesOffset + 7] = ChannelType.Discrete;
                            break;

                        default:

                            for (int ch = 0; ch < channelsPerTrack; ch++)
                            {
                                channelTypes[channelTypesOffset + ch] = ChannelType.Discrete;
                            }
                            break;
                    }

                    channelTypesOffset += channelsPerTrack;
                }
            }
        }

        private static bool IsHeaderValid(Stream hcaStream, out int headerSize)
        {
            BitReader bitReader = new BitReader(new BinaryReader(hcaStream).ReadBytes(8));

            headerSize = 0;
            if ((bitReader.Peek(32) & Mask) == StringToUInt32("HCA"))
            {
                bitReader.Skip(32 + 16);
                headerSize = bitReader.Read(16);

                return true;
            }

            return false;
        }

        private static int HeaderCeil2(int a, int b)
        {
            if (b < 1) return 0;
            return a / b + ((a % b) > 0 ? 1 : 0);
        }

        private static uint StringToUInt32(string value)
        {
            uint result = 0;
            int bytePos = 3;
            for (int i = 0; i < value.Length; i++)
            {
                result |= (uint)(value[i] << 8 * bytePos--);
            }
            return result;
        }
    }
}