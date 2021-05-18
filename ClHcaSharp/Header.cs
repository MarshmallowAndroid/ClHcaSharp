using System;
using System.IO;
using System.Text;
using static ClHcaSharp.Constants;

namespace ClHcaSharp
{
    public static class Header
    {
        public static bool IsHeaderValid(Stream hcaStream, out uint headerSize)
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

        public static void DecodeHeader(HcaContext hca, Stream hcaStream)
        {
            if (!IsHeaderValid(hcaStream, out uint headerSize))
                throw new InvalidDataException("Invalid HCA header.");

            BinaryReader binaryReader = new BinaryReader(hcaStream);
            binaryReader.BaseStream.Position = 0;

            BitReader bitReader = new BitReader(binaryReader.ReadBytes((int)headerSize));

            if ((bitReader.Peek(32) & Mask) == StringToUInt32("HCA"))
            {
                bitReader.Skip(32);
                hca.Version = bitReader.Read(16);
                hca.HeaderSize = bitReader.Read(16);

                headerSize -= 8;
            }
            else throw new InvalidDataException("Not an HCA file.");

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("fmt"))
            {
                bitReader.Skip(32);
                hca.ChannelCount = bitReader.Read(8);
                hca.SampleRate = bitReader.Read(24);
                hca.FrameCount = bitReader.Read(32);
                hca.EncoderDelay = bitReader.Read(16);
                hca.EncoderPadding = bitReader.Read(16);

                if (hca.ChannelCount < MinChannels || hca.ChannelCount > MaxChannels)
                    throw new InvalidDataException("Invalid channel count.");

                if (hca.FrameCount == 0)
                    throw new InvalidDataException("Frame count is zero.");

                if (hca.SampleRate < MinSampleRate || hca.SampleRate > MaxSampleRate)
                    throw new InvalidDataException("Invalid sample rate.");

                headerSize -= 16;
            }
            else throw new InvalidDataException("No format chunk.");

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("comp"))
            {
                bitReader.Skip(32);
                hca.FrameSize = bitReader.Read(16);
                hca.MinResolution = bitReader.Read(8);
                hca.MaxResolution = bitReader.Read(8);
                hca.TrackCount = bitReader.Read(8);
                hca.ChannelConfig = bitReader.Read(8);
                hca.TotalBandCount = bitReader.Read(8);
                hca.BaseBandCount = bitReader.Read(8);
                hca.StereoBandCount = bitReader.Read(8);
                hca.BandsPerHfrGroup = bitReader.Read(8);
                hca.MsStereo = bitReader.Read(8);
                hca.Reserved = bitReader.Read(8);

                headerSize -= 16;
            }
            else if (headerSize >= 0x0c && (bitReader.Peek(32) & Mask) == StringToUInt32("dec"))
            {
                bitReader.Skip(32);
                hca.FrameSize = bitReader.Read(16);
                hca.MinResolution = bitReader.Read(8);
                hca.MaxResolution = bitReader.Read(8);
                hca.TotalBandCount = bitReader.Read(8) + 1;
                hca.BaseBandCount = bitReader.Read(8) + 1;
                hca.TrackCount = bitReader.Read(4);
                hca.ChannelConfig = bitReader.Read(4);
                hca.StereoType = bitReader.Read(8);

                if (hca.StereoType == 0) hca.BaseBandCount = hca.TotalBandCount;
                hca.StereoBandCount = hca.TotalBandCount - hca.BaseBandCount;
                hca.BandsPerHfrGroup = 0;

                headerSize -= 12;
            }
            else throw new InvalidDataException("No compression or decode chunk.");

            if (headerSize >= 8 && (bitReader.Peek(32) & Mask) == StringToUInt32("vbr"))
            {
                bitReader.Skip(32);
                hca.VbrMaxFrameSize = bitReader.Read(16);
                hca.VbrNoiseLevel = bitReader.Read(16);

                if (hca.FrameSize != 0 || hca.VbrMaxFrameSize <= 8 || hca.VbrMaxFrameSize > 0x1FF)
                    throw new InvalidDataException("Invalid frame size.");

                headerSize -= 8;
            }

            if (headerSize >= 6 && (bitReader.Peek(32) & Mask) == StringToUInt32("ath"))
            {
                bitReader.Skip(32);
                hca.AthType = bitReader.Read(16);
            }
            else hca.AthType = (uint)((hca.Version < Version200) ? 1 : 0);

            if (headerSize >= 16 && (bitReader.Peek(32) & Mask) == StringToUInt32("loop"))
            {
                bitReader.Skip(32);
                hca.LoopStartFrame = bitReader.Read(32);
                hca.LoopEndFrame = bitReader.Read(32);
                hca.LoopStartDelay = bitReader.Read(16);
                hca.LoopEndPadding = bitReader.Read(16);

                hca.LoopFlag = 1;

                if (hca.LoopStartFrame < 0 || hca.LoopStartFrame > hca.LoopEndFrame || hca.LoopEndFrame >= hca.FrameCount)
                    throw new InvalidDataException("Invalid loop frames.");

                headerSize -= 16;
            }
            else
            {
                hca.LoopStartFrame = 0;
                hca.LoopEndFrame = 0;
                hca.LoopStartDelay = 0;
                hca.LoopEndPadding = 0;

                hca.LoopFlag = 0;
            }

            if (headerSize >= 6 && (bitReader.Peek(32) & Mask) == StringToUInt32("ciph"))
            {
                bitReader.Skip(32);
                hca.CiphType = bitReader.Read(16);

                if (hca.CiphType != 0 && hca.CiphType != 1 && hca.CiphType != 56)
                    throw new InvalidDataException("Invalid cipher type.");

                headerSize -= 6;
            }

            if (headerSize >= 8 && (bitReader.Peek(32) & Mask) == StringToUInt32("rva"))
            {
                bitReader.Skip(32);
                uint rvaVolumeInt = bitReader.Read(32);
                hca.RvaVolume = BitConverter.ToSingle(BitConverter.GetBytes(rvaVolumeInt));

                headerSize -= 8;
            }
            else hca.RvaVolume = 1.0f;

            if (headerSize >= 5 && (bitReader.Peek(32) & Mask) == StringToUInt32("comm"))
            {
                bitReader.Skip(32);
                hca.CommentLength = bitReader.Read(8);

                if (hca.CommentLength > headerSize) throw new InvalidDataException("Comment string out of bounds.");

                StringBuilder commentStringBuilder = new StringBuilder();

                for (int i = 0; i < hca.CommentLength; i++)
                {
                    commentStringBuilder.Append(bitReader.Read(8));
                }

                hca.Comment = commentStringBuilder.ToString();
            }
            else hca.CommentLength = 0;

            if (headerSize >= 4 && (bitReader.Peek(32) & Mask) == StringToUInt32("pad"))
            {
                headerSize -= (headerSize - 2);
            }

            if (hca.FrameSize < MinFrameSize || hca.FrameSize > MaxFrameSize)
                throw new InvalidDataException("Invalid frame size.");

            if (hca.Version <= Version200)
            {
                if (hca.MinResolution != 1 || hca.MaxResolution != 15)
                    throw new InvalidDataException("Incompatible resolution.");
            }

            if (hca.TrackCount == 0) hca.TrackCount = 1;

            if (hca.TrackCount > hca.ChannelCount) throw new InvalidDataException("Invalid track count.");

            if (hca.TotalBandCount > SamplesPerSubframe ||
                hca.BaseBandCount > SamplesPerSubframe ||
                hca.StereoBandCount > SamplesPerSubframe ||
                hca.BaseBandCount + hca.StereoBandCount > SamplesPerSubframe ||
                hca.BandsPerHfrGroup > SamplesPerSubframe)
                throw new InvalidDataException("Invalid bands.");

            hca.HfrGroupCount = HeaderCeil2(
                hca.TotalBandCount - hca.BaseBandCount - hca.StereoBandCount,
                hca.BandsPerHfrGroup);

            hca.AthCurve = Ath.Init(hca.AthType, hca.SampleRate);
            hca.CipherTable = Cipher.Init(hca.CiphType, hca.KeyCode);

            uint channelsPerTrack = hca.ChannelCount / hca.TrackCount;
            ChannelType[] channelTypes = new ChannelType[channelsPerTrack];

            if (hca.StereoBandCount > 0 && channelsPerTrack > 1)
            {
                for (int i = 0; i < hca.TrackCount; i++)
                {
                    switch (channelsPerTrack)
                    {
                        case 2:
                        case 3:
                            channelTypes[0] = ChannelType.StereoPrimary;
                            channelTypes[1] = ChannelType.StereoSecondary;
                            break;

                        case 4:
                            channelTypes[0] = ChannelType.StereoPrimary;
                            channelTypes[1] = ChannelType.StereoSecondary;
                            if (hca.ChannelConfig == 0)
                            {
                                channelTypes[2] = ChannelType.StereoPrimary;
                                channelTypes[3] = ChannelType.StereoSecondary;
                            }
                            break;

                        case 5:
                            channelTypes[0] = ChannelType.StereoPrimary;
                            channelTypes[1] = ChannelType.StereoSecondary;
                            if (hca.ChannelConfig <= 2)
                            {
                                channelTypes[3] = ChannelType.StereoPrimary;
                                channelTypes[4] = ChannelType.StereoSecondary;
                            }
                            break;

                        case 6:
                        case 7:
                            channelTypes[0] = ChannelType.StereoPrimary;
                            channelTypes[1] = ChannelType.StereoSecondary;
                            channelTypes[4] = ChannelType.StereoPrimary;
                            channelTypes[5] = ChannelType.StereoSecondary;
                            break;

                        case 8:
                            channelTypes[0] = ChannelType.StereoPrimary;
                            channelTypes[1] = ChannelType.StereoSecondary;
                            channelTypes[4] = ChannelType.StereoPrimary;
                            channelTypes[5] = ChannelType.StereoSecondary;
                            channelTypes[6] = ChannelType.StereoPrimary;
                            channelTypes[7] = ChannelType.StereoSecondary;
                            break;

                        default:
                            break;
                    }
                }
            }

            hca.Channels = new Channel[hca.ChannelCount];
            for (int i = 0; i < hca.ChannelCount; i++)
            {
                hca.Channels[i] = new Channel
                {
                    Type = channelTypes[i],
                    CodedCount =
                    channelTypes[i] != ChannelType.StereoSecondary ?
                    hca.BaseBandCount + hca.StereoBandCount :
                    hca.BaseBandCount
                };
            }

            hca.Random = DefaultRandom;

            if (hca.MsStereo > 0) throw new InvalidDataException();
            if (hca.HfrGroupCount > 0 && hca.Version == Version300)
                throw new InvalidDataException();
        }

        private static uint HeaderCeil2(uint a, uint b)
        {
            if (b < 1) return 0;
            return (uint)(a / b + ((a % b) > 0 ? 1 : 0));
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