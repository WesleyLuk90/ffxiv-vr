using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FfxivVR
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

        public unsafe static string ReadCString(byte* pointer)
        {
            return Marshal.PtrToStringUTF8((IntPtr)pointer)!;
        }
        public unsafe static void WriteCString(byte* pointer, string value, int maxLength)
        {
            var span = new Span<byte>(pointer, maxLength);
            Encoding.UTF8.GetBytes(value + "\0", span);
        }

        public unsafe static void WithStringPointer(string value, Action<IntPtr> block)
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
