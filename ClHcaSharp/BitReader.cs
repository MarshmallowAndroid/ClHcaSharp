using ClHcaSharp;

namespace ClHcaSharp
{
    public class BitReader
    {
        private static readonly uint[] mask32 = new uint[]
        {
            0xFFFFFFFF, 0x7FFFFFFF, 0x3FFFFFFF, 0x1FFFFFFF,
            0x0FFFFFFF, 0x07FFFFFF, 0x03FFFFFF, 0x01FFFFFF
        };

        private static readonly uint[] mask24 = new uint[]
        {
            0xFFFFFF, 0x7FFFFF, 0x3FFFFF, 0x1FFFFF,
            0x0FFFFF, 0x07FFFF, 0x03FFFF, 0x01FFFF
        };

        private static readonly uint[] mask16 = new uint[]
        {
            0xFFFF, 0x7FFF, 0x3FFF, 0x1FFF,
            0x0FFF, 0x07FF, 0x03FF, 0x01FF
        };

        private static readonly uint[] mask8 = new uint[]
        {
            0xFF, 0x7F, 0x3F, 0x1F,
            0x0F, 0x07, 0x03, 0x01
        };

        private readonly byte[] data;
        private readonly uint dataSize;
        private uint currentBit;

        public BitReader(byte[] data)
        {
            this.data = data;
            dataSize = (uint)data.Length * 8;
        }

        public uint Peek(uint bits)
        {
            uint bit = currentBit;
            uint bitsRemaining = bit & 7;
            uint size = dataSize;

            uint value = 0;

            if (bit + bits <= size)
            {
                uint bitOffset = bits + bitsRemaining;
                uint bitLeft = size - bit;

                if (bitLeft >= 32 && bitOffset >= 25)
                {
                    uint dataOffset = bit >> 3;
                    value = data[dataOffset];
                    value = (value << 8) | data[dataOffset + 1];
                    value = (value << 8) | data[dataOffset + 2];
                    value = (value << 8) | data[dataOffset + 3];
                    value &= mask32[bitsRemaining];
                    value >>= (int)(32 - bitsRemaining - bits);
                }
                else if (bitLeft >= 24 && bitOffset >= 17)
                {
                    uint dataOffset = bit >> 3;
                    value = data[dataOffset];
                    value = (value << 8) | data[dataOffset + 1];
                    value = (value << 8) | data[dataOffset + 2];
                    value &= mask24[bitsRemaining];
                    value >>= (int)(24 - bitsRemaining - bits);
                }
                else if (bitLeft >= 16 && bitOffset >= 9)
                {
                    uint dataOffset = bit >> 3;
                    value = data[dataOffset];
                    value = (value << 8) | data[dataOffset + 1];
                    value &= mask16[bitsRemaining];
                    value >>= (int)(16 - bitsRemaining - bits);
                }
                else
                {
                    uint dataOffset = bit >> 3;
                    value = data[dataOffset];
                    value &= mask8[bitsRemaining];
                    value >>= (int)(8 - bitsRemaining - bits);
                }

                return value;
            }

            return value;
        }

        public uint Read(uint bits)
        {
            uint value = Peek(bits);
            currentBit += bits;
            return value;
        }

        public void Skip(int bits)
        {
            currentBit = (uint)(currentBit + bits);
        }
    }
}