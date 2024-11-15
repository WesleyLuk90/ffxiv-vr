namespace FfxivVR.Tests;
public class NativeTests
{
    [Test()]
    public unsafe void WriteCString()
    {
        var bytes = new byte[128];
        fixed (byte* ptr = new Span<byte>(bytes))
        {
            Native.WriteCString(ptr, "foobar", 128);
            Assert.That(Native.ReadCString(ptr), Is.EqualTo("foobar"));
        }
        Assert.Throws<ArgumentException>(
            () =>
            {
                fixed (byte* ptr = new Span<byte>(bytes))
                {
                    Native.WriteCString(ptr, string.Concat(Enumerable.Repeat("foobar", 30)), 128);
                }
            }
        );
    }
}