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
        var dxgi = DXGI.GetApi(null);
        var guid = IDXGIFactory4.Guid;
        dxgi.CreateDXGIFactory(ref guid, (void**)&factory).D3D11Check("CreateDXGIFactory");

        IDXGIAdapter* adapter = null;
        factory->EnumAdapters(0, &adapter).D3D11Check("EnumAdapters");
        AdapterDesc desc;
        adapter->GetDesc(&desc).D3D11Check("GetDesc");

        var dx = D3D11.GetApi(null);
        ID3D11Device* device;
        ID3D11DeviceContext* deviceContext;
        var featureLevel = D3DFeatureLevel.Level110;
        dx.CreateDevice(adapter, D3DDriverType.Unknown, 0, (uint)CreateDeviceFlag.Debug, &featureLevel, 1, D3D11.SdkVersion, &device, null, &deviceContext).D3D11Check("CreateDevice");
        var logger = new Logger();
        var session = new VRSession(XR.GetApi(), logger, device);
        session.Initialize();
        while (!session.State.Exiting)
        {
            session.StartFrame(deviceContext);
            session.EndFrame(deviceContext);
        }
        session.Dispose();
    }

}
