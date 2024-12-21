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

        public static unsafe string ReadCString(byte* pointer)
        {
            return Marshal.PtrToStringUTF8((IntPtr)pointer)!;
        }
        public static unsafe string ReadCString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes).Trim('\0');
        }
        public static unsafe void WriteCString(byte* pointer, string value, int maxLength)
        {
            var span = new Span<byte>(pointer, maxLength);
            Encoding.UTF8.GetBytes(value + "\0", span);
        }
        public static unsafe void WithAnsiStringPointer(string value, Action<IntPtr> block)
        {
            var bytes = Encoding.ASCII.GetBytes(value + "\0");
            fixed (byte* ptr = new Span<byte>(bytes))
            {
                block((IntPtr)ptr);
            }
        }

        public static unsafe void WithStringPointer(string value, Action<IntPtr> block)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            fixed (byte* ptr = new Span<byte>(bytes))
            {
                block((IntPtr)ptr);
            }
        }
    }
}