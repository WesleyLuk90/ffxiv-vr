using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVr
{
    public static class Native
    {
        public static T[] CreateArray<T>(T t, uint count)
        {
            T[] array = new T[count];
            for (uint i = 0; i < count; i++)
            {
                array[i] = t;
            }
            return array;
        }

        public unsafe static String? ByteToString(byte* pointer)
        {
            return Marshal.PtrToStringUTF8((IntPtr)pointer);
        }
        public unsafe static void Write(byte* pointer, String value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                pointer[i] = bytes[i];
            }
            pointer[bytes.Length] = 0;
        }
    }
}
