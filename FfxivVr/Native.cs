using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FfxivVR
{
    static class Native
    {
        internal static T[] CreateArray<T>(T t, uint count)
        {
            T[] array = new T[count];
            for (uint i = 0; i < count; i++)
            {
                array[i] = t;
            }
            return array;
        }

        internal unsafe static string ReadCString(byte* pointer)
        {
            return Marshal.PtrToStringUTF8((IntPtr)pointer)!;
        }
        internal unsafe static void WriteCString(byte* pointer, string value, int maxLength)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var toWrite = Math.Min(maxLength - 1, bytes.Length);
            for (int i = 0; i < toWrite; i++)
            {
                pointer[i] = bytes[i];
            }
            pointer[toWrite] = 0;
        }

        internal unsafe static void WithStringPointer(string value, Action<IntPtr> block)
        {
            var bytes = Marshal.StringToHGlobalAnsi(value);
            try
            {
                block(bytes);
            }
            finally
            {
                Marshal.FreeHGlobal(bytes);
            }
        }
    }
}
