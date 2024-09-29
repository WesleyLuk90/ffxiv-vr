using Silk.NET.Core;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Linq;

namespace FfxivVR;
public unsafe class VRSystem : IDisposable
{
    internal Instance Instance = new Instance();
    internal Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;
    internal ulong SystemId;

    private XR xr;
    private ID3D11Device* device;
    private Logger logger;

    public VRSystem(XR xr, ID3D11Device* device, Logger logger)
    {
        this.xr = xr;
        this.device = device;
        this.logger = logger;
    }

    public class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");

    public void Initialize()
    {
        ApplicationInfo appInfo = new ApplicationInfo(applicationVersion: 1, engineVersion: 1, apiVersion: 1UL << 48);
        appInfo.SetApplicationName("FFXIV VR");

        var extensions = xr.GetInstanceExtensionProperties(layerName: null);
        var dx11Extension = extensions.Where(p => p.GetExtensionName() == "XR_KHR_D3D11_enable")
            .First();

        InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0, enabledExtensionCount: 1, enabledExtensionNames: &dx11Extension.ExtensionName);
        createInfo.ApplicationInfo = appInfo;


        xr.CreateInstance(&createInfo, ref Instance).CheckResult("CreateInstance");

        var instanceProperties = new InstanceProperties(next: null);
        xr.GetInstanceProperties(Instance, &instanceProperties).CheckResult("GetInstanceProperties");

        logger.Debug($"Runtime Name {instanceProperties.GetRuntimeName()} Runtime Version {instanceProperties.RuntimeVersion}");

        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);

        var result = xr.GetSystem(Instance, &getInfo, ref SystemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            logger.Error("Headset not found");
            throw new FormFactorUnavailableException();
        }
        result.CheckResult("GetSystem");


        PfnVoidFunction function = new PfnVoidFunction();
        xr.GetInstanceProcAddr(Instance, "xrGetD3D11GraphicsRequirementsKHR", &function).CheckResult("GetInstanceProcAddr");
        var getRequirements = (delegate* unmanaged[Cdecl]<Instance, ulong, GraphicsRequirementsD3D11KHR*, Result>)function.Handle;

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        getRequirements(Instance, SystemId, &requirements).CheckResult("xrGetD3D11GraphicsRequirementsKHR");

        logger.Debug($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");

        var binding = new GraphicsBindingD3D11KHR(device: device);

        var sessionInfo = new SessionCreateInfo(systemId: SystemId, createFlags: 0, next: &binding);

        xr.CreateSession(Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");
    }

    public void Dispose()
    {
        xr.DestroySession(Session).LogResult("DestroySession", logger);
        xr.DestroyInstance(Instance).LogResult("DestroyInstance", logger);
    }
}
