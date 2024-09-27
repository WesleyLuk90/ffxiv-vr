using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Core;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR;
unsafe class VRSystem : IDisposable
{
    Instance instance = new Instance();
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

        uint extensionsCount;
        xr.EnumerateInstanceExtensionProperties((byte*)null, 0, &extensionsCount, null);
        logger.Info($"Found {extensionsCount} extensions");
        var properties = Native.CreateArray(new ExtensionProperties(next: null), extensionsCount);
        xr.EnumerateInstanceExtensionProperties((byte*)null, extensionsCount, &extensionsCount, properties);

        var dx11Extension = properties.Where(p => p.GetExtensionName() == "XR_KHR_D3D11_enable")
            .First();

        InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0, enabledExtensionCount: 1, enabledExtensionNames: &dx11Extension.ExtensionName);
        createInfo.ApplicationInfo = appInfo;


        fixed (Instance* i = &instance)
        {
            xr.CreateInstance(&createInfo, i).CheckResult("CreateInstance");
        }

        var instanceProperties = new InstanceProperties(next: null);
        xr.GetInstanceProperties(instance, &instanceProperties).CheckResult("GetInstanceProperties");

        logger.Info($"Runtime Name {instanceProperties.GetRuntimeName()} Runtime Version {instanceProperties.RuntimeVersion}");

        ulong systemId;
        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);
        var result = xr.GetSystem(instance, &getInfo, &systemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            throw new FormFactorUnavailableException();
        }
        result.CheckResult("GetSystem");


        PfnVoidFunction function = new PfnVoidFunction();
        xr.GetInstanceProcAddr(instance, "xrGetD3D11GraphicsRequirementsKHR", &function).CheckResult("GetInstanceProcAddr");
        var getRequirements = (delegate* unmanaged[Cdecl]<Instance, ulong, GraphicsRequirementsD3D11KHR*, Result>)function.Handle;

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        getRequirements(instance, systemId, &requirements).CheckResult("xrGetD3D11GraphicsRequirementsKHR");

        logger.Info($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");

        if (device == null)
        {
            throw new Exception("Device was null");
        }
        if (device->D3D11Forwarder == null)
        {
            throw new Exception("D3D11Forwarder was null");
        }
        var binding = new GraphicsBindingD3D11KHR(device: device->D3D11Forwarder);

        var sessionInfo = new SessionCreateInfo(systemId: systemId, createFlags: 0, next: &binding);

        fixed (Session* s = &session)
        {
            xr.CreateSession(instance, ref sessionInfo, s).CheckResult("CreateSession");
        }
    }

    public void Dispose()
    {
        xr.DestroySession(session);
        xr.DestroyInstance(instance);
    }
}
