using System;
using System.Collections.Generic;
using System.Text;

namespace ClHcaSharp
{
    internal static class Util
    {
        public static float UInt32ToSingle(uint value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);

        public static void Fill<T>(Array array, T value, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                array.SetValue(value, i + startIndex);
            }
        }
    }
}
