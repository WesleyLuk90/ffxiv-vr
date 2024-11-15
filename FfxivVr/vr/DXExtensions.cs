using System;

namespace FfxivVR;

public static class DXExtensions
{
    public static void D3D11Check(this int result, string action)
    {
        switch ((uint)result)
        {
            case 0:
                {
                    return;
                }
            case 0x80070057:
                {
                    throw new Exception($"D3D11 {action} call failed E_INVALIDARG (0x80070057)");
                }
            default:
                {
                    throw new Exception($"D3D11 {action} call failed 0x{result.ToString("x")}");
                }

        }
    }
}