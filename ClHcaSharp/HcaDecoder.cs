using System;
using System.IO;
using static ClHcaSharp.Constants;
using static ClHcaSharp.Tables;

namespace ClHcaSharp
{
    public class HcaDecoder
    {
        private readonly BinaryReader binaryReader;
        private readonly HcaContext hca;

        public HcaDecoder(Stream hcaStream, ulong key)
        {
            hca = new HcaContext();

            HcaHeader.DecodeHeader(hca, hcaStream);
            SetKey(key);

            //var testFile = new FileStream("test.raw", FileMode.Create);
            //var testFileWriter = new BinaryWriter(testFile);

            //for (int i = 0; i < hca.FrameCount; i++)
            //{
            //    DecodeBlock(binaryReader.ReadBytes((int)hca.FrameSize));

            //    for (int subframe = 0; subframe < SubframesPerFrame; subframe++)
            //    {
            //        for (int sample = 0; sample < SamplesPerSubframe; sample++)
            //        {
            //            for (int channel = 0; channel < hca.ChannelCount; channel++)
            //            {
            //                float f = hca.Channels[channel].Wave[subframe][sample];
            //                if (f > 1.0f) f = 1.0f;
            //                else if (f < -1.0f) f = -1.0f;
            //                short s = (short)(32767 * f);
            //                testFileWriter.Write(s);
            //            }
            //        }
            //    }
            //}
        }

        public HcaInfo GetInfo()
        {
            HcaInfo info = new HcaInfo();
            info.Version = hca.Version;
            info.HeaderSize = hca.HeaderSize;
            info.SamplingRate = hca.SampleRate;
            info.ChannelCount = hca.ChannelCount;
            info.BlockSize = hca.FrameSize;
            info.BlockCount = hca.FrameCount;
            info.EncoderDelay = hca.EncoderDelay;
            info.EncoderPadding = hca.EncoderPadding;
            info.LoopEnabled = hca.LoopFlag;
            info.LoopStartBlock = hca.LoopStartFrame;
            info.LoopEndBlock = hca.LoopEndFrame;
            info.LoopStartDelay = hca.LoopStartDelay;
            info.LoopEndPadding = hca.LoopEndPadding;
            info.SamplesPerBlock = SamplesPerFrame;
            info.Comment = hca.Comment;
            info.EncryptionEnabled = (uint)(hca.CiphType == 56 ? 1 : 0);
            return info;
        }

        public void SetKey(ulong key)
        {
            hca.KeyCode = key;
            hca.CipherTable = Cipher.Init(hca.CiphType, hca.KeyCode);
        }

        public void ReadSamples16(short[][] samples)
        {
            for (int subframe = 0; subframe < SubframesPerFrame; subframe++)
            {
                for (int sample = 0; sample < SamplesPerSubframe; sample++)
                {
                    for (int channel = 0; channel < hca.ChannelCount; channel++)
                    {
                        float f = hca.Channels[channel].Wave[subframe][sample];
                        int sInt = (int)(32768 * f);
                        if (sInt > 32767)
                            sInt = 32767;
                        else if (sInt < -32767)
                            sInt = -32767;
                        samples[channel][(SamplesPerSubframe * subframe) + sample] = (short)sInt;
                    }
                }
            }
        }

        public void DecodeBlock(byte[] data)
        {
            if (data.Length < hca.FrameSize)
                throw new ArgumentException("Data is less than expected frame size.");

            BitReader bitReader = new BitReader(data);

            ushort sync = (ushort)bitReader.Read(16);
            if (sync != 0xFFFF) throw new InvalidDataException("Sync error.");

            if (Crc.Crc16Checksum(data) > 0) throw new InvalidDataException("Checksum error.");

            Cipher.Decrypt(hca.CipherTable, data);

            uint frameAcceptableNoiseLevel = bitReader.Read(9);
            uint frameEvaluationBoundary = bitReader.Read(7);

            uint packedNoiseLevel = (frameAcceptableNoiseLevel << 8) - frameEvaluationBoundary;

            for (int channel = 0; channel < hca.ChannelCount; channel++)
            {
                UnpackScaleFactors(hca.Channels[channel], bitReader, hca.HfrGroupCount, hca.Version);
                UnpackIntensity(hca.Channels[channel], bitReader, hca.HfrGroupCount, hca.Version);
                CalculateResolution(hca.Channels[channel], packedNoiseLevel, hca.AthCurve, hca.MinResolution, hca.MaxResolution);
                CalculateGain(hca.Channels[channel]);
            }

            for (int subframe = 0; subframe < SubframesPerFrame; subframe++)
            {
                for (int channel = 0; channel < hca.ChannelCount; channel++)
                {
                    DequantizeCoefficients(hca.Channels[channel], bitReader);
                }

                for (int channel = 0; channel < hca.ChannelCount; channel++)
                {
                    uint random = hca.Random;
                    ReconstructNoise(hca.Channels[channel], hca.MinResolution, hca.MsStereo, ref random);
                    hca.Random = random;
                    ReconstructHighFrequency(hca.Channels[channel], hca.HfrGroupCount, hca.BandsPerHfrGroup,
                                             hca.StereoBandCount, hca.BaseBandCount, hca.TotalBandCount, hca.Version);
                }

                if (hca.StereoBandCount > 0)
                {
                    for (int ch = 0; ch < hca.ChannelCount - 1; ch++)
                    {
                        ApplyIntensityStereo(hca.Channels, ch * 2, subframe, hca.BaseBandCount, hca.TotalBandCount);
                        ApplyMsStereo(hca.Channels, ch * 2, hca.MsStereo, hca.BaseBandCount, hca.TotalBandCount);
                    }
                }

                for (int channel = 0; channel < hca.ChannelCount; channel++)
                {
                    ImdctTransform(hca.Channels[channel], subframe);
                }
            }
        }

        private static void UnpackScaleFactors(Channel channel, BitReader bitReader, uint hfrGroupCount, uint version)
        {
            uint csCount = channel.CodedCount;
            uint extraCount;
            byte deltaBits = (byte)bitReader.Read(3);

            if (channel.Type == ChannelType.StereoSecondary || hfrGroupCount <= 0 || version <= Version200)
                extraCount = 0;
            else
            {
                extraCount = hfrGroupCount;
                csCount += extraCount;

                if (csCount > SamplesPerSubframe) throw new InvalidDataException("Invalid scale count.");
            }

            if (deltaBits >= 6)
            {
                for (int i = 0; i < csCount; i++)
                {
                    channel.ScaleFactors[i] = (byte)bitReader.Read(6);
                }
            }
            else if (deltaBits > 0)
            {
                byte expectedDelta = (byte)((1 << deltaBits) - 1);
                byte value = (byte)bitReader.Read(6);

                channel.ScaleFactors[0] = value;
                for (int i = 1; i < csCount; i++)
                {
                    byte delta = (byte)bitReader.Read(deltaBits);

                    if (delta == expectedDelta)
                        value = (byte)bitReader.Read(6);
                    else
                    {
                        int scaleFactorTest = value + (delta - (expectedDelta >> 1));
                        if (scaleFactorTest < 0 || scaleFactorTest >= 64)
                            throw new InvalidDataException("Invalid scale factor.");

                        value = (byte)(value - (expectedDelta >> 1) + delta);
                        value = (byte)(value & 0x3F);
                    }

                    channel.ScaleFactors[i] = value;
                }
            }
            else
            {
                for (int i = 0; i < SamplesPerSubframe; i++)
                {
                    channel.ScaleFactors[i] = 0;
                }
            }

            for (int i = 0; i < extraCount; i++)
            {
                channel.ScaleFactors[SamplesPerSubframe - 1 - i] = channel.ScaleFactors[csCount - i];
            }
        }

        private static void UnpackIntensity(Channel channel, BitReader bitReader, uint hfrGroupCount, uint version)
        {
            if (channel.Type == ChannelType.StereoSecondary)
            {
                if (version <= Version200)
                {
                    byte value = (byte)bitReader.Peek(4);

                    channel.Intensity[0] = value;
                    if (value < 15)
                    {
                        bitReader.Skip(4);
                        for (int i = 1; i < SubframesPerFrame; i++)
                        {
                            channel.Intensity[i] = (byte)bitReader.Read(4);
                        }
                    }
                }
                else
                {
                    byte value = (byte)bitReader.Peek(4);
                    byte deltaBits;

                    if (value < 15)
                    {
                        bitReader.Skip(4);

                        deltaBits = (byte)bitReader.Read(2);

                        channel.Intensity[0] = value;
                        if (deltaBits == 3)
                        {
                            for (int i = 1; i < SubframesPerFrame; i++)
                            {
                                channel.Intensity[i] = (byte)bitReader.Read(4);
                            }
                        }
                        else
                        {
                            byte bMax = (byte)((2 << deltaBits) - 1);
                            byte bits = (byte)(deltaBits + 1);

                            for (int i = 1; i < SubframesPerFrame; i++)
                            {
                                byte delta = (byte)bitReader.Read(bits);
                                if (delta == bMax)
                                    value = (byte)bitReader.Read(4);
                                else
                                {
                                    value = (byte)(value - (bMax >> 1) + delta);
                                    if (value > 15)
                                        throw new InvalidDataException("Intensity value out of range.");
                                }

                                channel.Intensity[i] = value;
                            }
                        }
                    }
                    else
                    {
                        bitReader.Skip(4);
                        for (int i = 0; i < SubframesPerFrame; i++)
                        {
                            channel.Intensity[i] = 7;
                        }
                    }
                }
            }
            else
            {
                if (version <= Version200)
                {
                    byte[] hfrScales = channel.ScaleFactors;
                    int hfrScalesOffset = (int)(128 - hfrGroupCount);

                    for (int i = 0; i < hfrGroupCount; i++)
                    {
                        hfrScales[i] = (byte)bitReader.Read(6);
                    }
                }
            }
        }

        private static void CalculateResolution(Channel channel, uint packedNoiseLevel, byte[] athCurve, uint minResolution,
                                        uint maxResolution)
        {
            uint crCount = channel.CodedCount;
            uint noiseCount = 0;
            uint validCount = 0;

            for (int i = 0; i < crCount; i++)
            {
                byte newResolution = 0;
                byte scaleFactor = channel.ScaleFactors[i];

                if (scaleFactor > 0)
                {
                    int noiseLevel = (int)(athCurve[i] + ((packedNoiseLevel + i) >> 8));
                    int curvePosition = noiseLevel + 1 - ((5 * scaleFactor) >> 1);

                    if (curvePosition < 0)
                        newResolution = 15;
                    else if (curvePosition <= 65)
                        newResolution = InvertTable[curvePosition];
                    else
                        newResolution = 0;

                    if (newResolution > maxResolution)
                        newResolution = (byte)maxResolution;
                    else if (newResolution < minResolution)
                        newResolution = (byte)minResolution;

                    if (newResolution < 1)
                    {
                        channel.Noises[noiseCount] = (byte)i;
                        noiseCount++;
                    }
                    else
                    {
                        channel.Noises[SamplesPerSubframe - 1 - validCount] = (byte)i;
                        validCount++;
                    }
                }
                channel.Resolution[i] = newResolution;
            }

            channel.NoiseCount = noiseCount;
            channel.ValidCount = validCount;

            Array.Clear(channel.Resolution, (int)crCount, (int)(SamplesPerSubframe - crCount));
        }

        private static void CalculateGain(Channel channel)
        {
            uint cgCount = channel.CodedCount;
            for (int i = 0; i < cgCount; i++)
            {
                float scaleFactorScale = GetDequantizerScalingTableValue(channel.ScaleFactors[i]);
                float resolutionScale = GetDequantizerRangeTableValue(channel.Resolution[i]);
                channel.Gain[i] = scaleFactorScale * resolutionScale;
            }
        }

        private static void DequantizeCoefficients(Channel channel, BitReader bitReader)
        {
            uint ccCount = channel.CodedCount;

            for (int i = 0; i < ccCount; i++)
            {
                float qc;
                byte resolution = channel.Resolution[i];
                byte bits = MaxBitTable[resolution];
                uint code = bitReader.Read(bits);

                if (resolution > 7)
                {
                    int signedCode = (int)((1 - ((code & 1) << 1)) * (code >> 1));
                    if (signedCode == 0)
                        bitReader.Skip(-1);
                    qc = signedCode;
                }
                else
                {
                    int index = (int)((resolution << 4) + code);
                    int skip = ReadBitTable[index] - bits;
                    bitReader.Skip(skip);
                    qc = ReadValueTable[index];
                }

                channel.Spectra[i] = channel.Gain[i] * qc;
            }

            Array.Clear(channel.Spectra, (int)ccCount, (int)(SamplesPerSubframe - ccCount));
        }

        private static void ReconstructNoise(Channel channel, uint minResolution, uint msStereo, ref uint random)
        {
            if (minResolution > 0) return;
            if (channel.ValidCount <= 0 || channel.NoiseCount <= 0) return;
            if (msStereo != 0 && channel.Type == ChannelType.StereoPrimary) return;

            int randomIndex;
            int noiseIndex;
            int validIndex;
            int sfNoise;
            int sfValid;
            int scIndex;

            for (int i = 0; i < channel.NoiseCount; i++)
            {
                random = 0x343FD * random + 0x269EC3;

                randomIndex = (int)(SamplesPerSubframe - channel.ValidCount + (((random & 0x7FFF) * channel.ValidCount) >> 15));

                noiseIndex = channel.Noises[i];
                validIndex = channel.Noises[randomIndex];

                sfNoise = channel.ScaleFactors[noiseIndex];
                sfValid = channel.ScaleFactors[validIndex];
                scIndex = (sfNoise - sfValid + 62) & ~((sfNoise - sfValid + 62) >> 31);

                channel.Spectra[noiseIndex] = GetScaleConversionTableValue(scIndex) * channel.Spectra[validIndex];
            }
        }

        private static void ReconstructHighFrequency(Channel channel, uint hfrGroupCount, uint bandsPerHfrGroup,
                                             uint stereoBandCount, uint baseBandCount, uint totalBandCount, uint version)
        {
            if (bandsPerHfrGroup == 0) return;
            if (channel.Type == ChannelType.StereoSecondary) return;

            int groupLimit;
            int startBand = (int)(stereoBandCount + baseBandCount);
            int highBand = startBand;
            int lowBand = startBand - 1;
            int scIndex;

            uint hfrScalesOffset = 128 - hfrGroupCount;
            byte[] hfrScales = channel.ScaleFactors;

            if (version <= Version200)
                groupLimit = (int)hfrGroupCount;
            else
            {
                groupLimit = (int)(hfrGroupCount >= 0 ? hfrGroupCount : hfrGroupCount + 1);
                groupLimit >>= 1;
            }

            for (int group = 0; group < hfrGroupCount; group++)
            {
                int lowBandSub = group < groupLimit ? 1 : 0;

                for (int i = 0; i < bandsPerHfrGroup; i++)
                {
                    if (highBand >= totalBandCount || lowBand < 0) break;

                    scIndex = hfrScales[hfrScalesOffset + group];
                    scIndex &= ~(scIndex >> 31);

                    channel.Spectra[highBand] = GetScaleConversionTableValue(scIndex) * channel.Spectra[lowBand];

                    highBand++;
                    lowBand -= lowBandSub;
                }
            }

            channel.Spectra[highBand - 1] = 0.0f;
        }

        private static void ApplyIntensityStereo(Channel[] channelPair, int channelOffset, int subframe, uint baseBandCount, uint totalBandCount)
        {
            if (channelPair[channelOffset + 0].Type != ChannelType.StereoPrimary) return;

            float ratioL = GetIntensityRatioTableValue(channelPair[channelOffset + 1].Intensity[subframe]);
            float ratioR = 2.0f - ratioL;
            float[] spectraL = channelPair[channelOffset + 0].Spectra;
            float[] spectraR = channelPair[channelOffset + 1].Spectra;

            for (int band = (int)baseBandCount; band < totalBandCount; band++)
            {
                float coefL = spectraL[band] * ratioL;
                float coefR = spectraR[band] * ratioR;
                spectraL[band] = coefL;
                spectraR[band] = coefR;
            }
        }

        private static void ApplyMsStereo(Channel[] channelPair, int channelOffset, uint msStereo, uint baseBandCount, uint totalBandCount)
        {
            if (msStereo != 0) return;
            if (channelPair[channelOffset + 0].Type != ChannelType.StereoPrimary) return;

            float ratio = BitConverter.Int32BitsToSingle(0x3F3504F3);
            float[] spectraL = channelPair[channelOffset + 0].Spectra;
            float[] spectraR = channelPair[channelOffset + 1].Spectra;

            for (int band = (int)baseBandCount; band < totalBandCount; band++)
            {
                float coefL = (spectraL[band] + spectraR[band]) * ratio;
                float coefR = (spectraL[band] - spectraR[band]) * ratio;
                spectraL[band] = coefL;
                spectraR[band] = coefR;
            }
        }

        private static void ImdctTransform(Channel channel, int subframe)
        {
            const uint size = SamplesPerSubframe;
            const uint half = SamplesPerSubframe / 2;
            const uint mdctBits = MdctBits;

            uint count1 = 1;
            uint count2 = half;
            float[] temp1 = channel.Spectra;
            float[] temp2 = channel.Temp;
            int temp1Index = 0;

            for (int i = 0; i < mdctBits; i++)
            {
                float[] swap;
                float[] d1 = temp2;
                float[] d2 = temp2;
                int d1Index = 0;
                int d2Index = (int)count2;

                for (int j = 0; j < count1; j++)
                {
                    for (int k = 0; k < count2; k++)
                    {
                        float a = temp1[temp1Index++];
                        float b = temp1[temp1Index++];
                        d1[d1Index++] = a + b;
                        d2[d2Index++] = a - b;
                    }

                    d1Index += (int)count2;
                    d2Index += (int)count2;
                }

                temp1Index = 0;
                swap = temp1;
                temp1 = temp2;
                temp2 = swap;

                count1 <<= 1;
                count2 >>= 1;
            }

            count1 = half;
            count2 = 1;
            temp1 = channel.Temp;
            temp2 = channel.Spectra;

            for (int i = 0; i < mdctBits; i++)
            {
                int sinTableIndex = 0;
                int cosTableIndex = 0;

                float[] swap;
                float[] d1 = temp2;
                float[] d2 = temp2;
                float[] s1 = temp1;
                float[] s2 = temp1;
                int d1Index = 0;
                int d2Index = (int)(count2 * 2 - 1);
                int s1Index = 0;
                int s2Index = (int)count2;

                for (int j = 0; j < count1; j++)
                {
                    for (int k = 0; k < count2; k++)
                    {
                        float a = s1[s1Index++];
                        float b = s2[s2Index++];
                        float sin = GetSinTableValue(i, sinTableIndex++);
                        float cos = GetCosTableValue(i, cosTableIndex++);
                        d1[d1Index++] = a * sin - b * cos;
                        d2[d2Index--] = a * cos + b * sin;
                    }

                    s1Index += (int)count2;
                    s2Index += (int)count2;
                    d1Index += (int)count2;
                    d2Index += (int)count2 * 3;
                }

                swap = temp1;
                temp1 = temp2;
                temp2 = swap;

                count1 >>= 1;
                count2 <<= 1;
            }

            float[] dct = channel.Spectra;
            float[] prev = channel.ImdctPrevious;
            for (int i = 0; i < half; i++)
            {
                channel.Wave[subframe][i] = GetImdctWindowValue(i) * dct[i + half] + prev[i];
                channel.Wave[subframe][i + half] = GetImdctWindowValue((int)(i + half)) * dct[size - 1 - i] - prev[i + half];
                channel.ImdctPrevious[i] = GetImdctWindowValue((int)(size - 1 - i)) * dct[half - i - 1];
                channel.ImdctPrevious[i + half] = GetImdctWindowValue((int)(half - i - 1)) * dct[i];
            }
        }
    }
}