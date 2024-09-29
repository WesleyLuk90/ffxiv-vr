using FfxivVR;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.OpenXR;

namespace StandaloneMain;

unsafe internal static class Program
{
    static void Main()
    {
        IDXGIFactory4* factory = null;
        var dxgi = DXGI.GetApi();
        var guid = IDXGIFactory4.Guid;
        D3D11Check(dxgi.CreateDXGIFactory(ref guid, (void**)&factory));

        IDXGIAdapter* adapter = null;
        D3D11Check(factory->EnumAdapters(0, &adapter));
        AdapterDesc desc;
        D3D11Check(adapter->GetDesc(&desc));

        var dx = D3D11.GetApi();
        ID3D11Device* device;
        ID3D11DeviceContext* deviceContext;
        var featureLevel = D3DFeatureLevel.Level110;
        D3D11Check(dx.CreateDevice(adapter, D3DDriverType.Unknown, 0, (uint)CreateDeviceFlag.Debug, &featureLevel, 1, D3D11.SdkVersion, &device, null, &deviceContext));
        var logger = new Logger();
        var vr = new VRSystem(XR.GetApi(), device, logger);
        vr.Initialize();
        vr.Dispose();
    }

    static void D3D11Check(int result)
    {
        switch ((uint)result)
        {
            case 0:
                {
                    return;
                }
            case 0x80070057:
                {
                    throw new Exception($"D3D11 call failed E_INVALIDARG (0x80070057)");
                    break;
                }
            default:
                {
                    throw new Exception($"D3D11 call failed 0x{result.ToString("x")}");
                }

        }
    }
}
