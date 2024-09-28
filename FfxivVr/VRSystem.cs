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
    internal Instance Instance = new Instance();
    internal Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;
    internal ulong SystemId;

    private XR xR;
    private Device* device;
    private Logger logger;

    internal VRSystem(XR xr, Device* device, Logger logger)
    {
        this.xR = xr;
        this.device = device;
        this.logger = logger;
    }

    class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");

    internal void Initialize()
    {
        ApplicationInfo appInfo = new ApplicationInfo(applicationVersion: 1, engineVersion: 1, apiVersion: 1UL << 48);
        appInfo.SetApplicationName("FFXIV VR");

        uint extensionsCount;
        xR.EnumerateInstanceExtensionProperties((byte*)null, 0, &extensionsCount, null);
        logger.Debug($"Found {extensionsCount} extensions");
        var properties = Native.CreateArray(new ExtensionProperties(next: null), extensionsCount);
        xR.EnumerateInstanceExtensionProperties((byte*)null, extensionsCount, &extensionsCount, properties);

        var dx11Extension = properties.Where(p => p.GetExtensionName() == "XR_KHR_D3D11_enable")
            .First();

        InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0, enabledExtensionCount: 1, enabledExtensionNames: &dx11Extension.ExtensionName);
        createInfo.ApplicationInfo = appInfo;


        xR.CreateInstance(&createInfo, ref Instance).CheckResult("CreateInstance");

        var instanceProperties = new InstanceProperties(next: null);
        xR.GetInstanceProperties(Instance, &instanceProperties).CheckResult("GetInstanceProperties");

        logger.Debug($"Runtime Name {instanceProperties.GetRuntimeName()} Runtime Version {instanceProperties.RuntimeVersion}");

        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);

        var result = xR.GetSystem(Instance, &getInfo, ref SystemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            logger.Error("Headset not found");
            throw new FormFactorUnavailableException();
        }
        result.CheckResult("GetSystem");


        PfnVoidFunction function = new PfnVoidFunction();
        xR.GetInstanceProcAddr(Instance, "xrGetD3D11GraphicsRequirementsKHR", &function).CheckResult("GetInstanceProcAddr");
        var getRequirements = (delegate* unmanaged[Cdecl]<Instance, ulong, GraphicsRequirementsD3D11KHR*, Result>)function.Handle;

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        getRequirements(Instance, SystemId, &requirements).CheckResult("xrGetD3D11GraphicsRequirementsKHR");

        logger.Debug($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");

        if (device == null)
        {
            throw new Exception("Device was null");
        }
        if (device->D3D11Forwarder == null)
        {
            throw new Exception("D3D11Forwarder was null");
        }
        var binding = new GraphicsBindingD3D11KHR(device: device->D3D11Forwarder);

        var sessionInfo = new SessionCreateInfo(systemId: SystemId, createFlags: 0, next: &binding);

        xR.CreateSession(Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");
    }

    public void Dispose()
    {
        xR.DestroySession(Session);
        xR.DestroyInstance(Instance);
    }
}
