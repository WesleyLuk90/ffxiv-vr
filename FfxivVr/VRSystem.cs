using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR;
unsafe class VRSystem : IDisposable
{
    Instance xrInstance = new Instance();
    Session session = new Session();

    private XR xr;
    private Device* device;
    private Logger logger;

    internal VRSystem(XR xr, Device* device, Logger logger)
    {
        this.xr = xr;
        this.device = device;
        this.logger = logger;
    }

    class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");

    internal void Initialize()
    {
        ApplicationInfo appInfo = new ApplicationInfo(applicationVersion: 1, engineVersion: 1, apiVersion: 1UL << 48);
        appInfo.SetApplicationName("FFXIV VR");
        InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0);
        createInfo.ApplicationInfo = appInfo;

        fixed (Instance* instance = &xrInstance)
        {
            xr.CreateInstance(&createInfo, instance).CheckResult("CreateInstance");
        }

        var instanceProperties = new InstanceProperties(next: null);
        xr.GetInstanceProperties(xrInstance, &instanceProperties).CheckResult("GetInstanceProperties");

        logger.Info($"Runtime Name {instanceProperties.GetRuntimeName()} Runtime Version {instanceProperties.RuntimeVersion}");

        ulong systemId;
        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);
        var result = xr.GetSystem(xrInstance, &getInfo, &systemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            throw new FormFactorUnavailableException();
        }
        result.CheckResult("GetSystem");
        var binding = new GraphicsBindingD3D11KHR(device: device);

        var sessionInfo = new SessionCreateInfo(systemId: systemId, createFlags: 0, next: &binding);

        fixed (Session* s = &session)
        {
            xr.CreateSession(xrInstance, ref sessionInfo, s).CheckResult("CreateSession");
        }
    }
    public void Dispose()
    {
        xr.DestroySession(session);
        xr.DestroyInstance(xrInstance);
    }
}
