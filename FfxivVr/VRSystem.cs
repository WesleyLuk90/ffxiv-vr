using Silk.NET.Core;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Diagnostics;
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
        var names = string.Join(",", extensions.Select(e => e.GetExtensionName()).ToList());

        logger.Debug($"Available extensions ({extensions.Count}): {names}");

        var dx11Extension = extensions
            .Where(p => p.GetExtensionName() == "XR_KHR_D3D11_enable")
            .First();
        var performanceCounterExtension = extensions
            .Where(p => p.GetExtensionName() == "XR_KHR_win32_convert_performance_counter_time")
            .First();

        var extensionsToEnable = new byte*[] { dx11Extension.ExtensionName, performanceCounterExtension.ExtensionName };
        fixed (byte** ptr = &extensionsToEnable[0])
        {
            InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0, enabledExtensionCount: 2, enabledExtensionNames: ptr);
            createInfo.ApplicationInfo = appInfo;


            xr.CreateInstance(&createInfo, ref Instance).CheckResult("CreateInstance");
        }

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

        PfnVoidFunction getRequirementsPointer = new PfnVoidFunction();
        xr.GetInstanceProcAddr(Instance, "xrGetD3D11GraphicsRequirementsKHR", &getRequirementsPointer).CheckResult("GetInstanceProcAddr");
        var getRequirements = (delegate* unmanaged[Cdecl]<Instance, ulong, GraphicsRequirementsD3D11KHR*, Result>)getRequirementsPointer.Handle;

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        getRequirements(Instance, SystemId, &requirements).CheckResult("xrGetD3D11GraphicsRequirementsKHR");
        logger.Debug($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");


        var binding = new GraphicsBindingD3D11KHR(device: device);

        PfnVoidFunction performanceToTimePointer = new PfnVoidFunction();
        xr.GetInstanceProcAddr(Instance, "xrConvertWin32PerformanceCounterToTimeKHR", &performanceToTimePointer).CheckResult("GetInstanceProcAddr");
        performanceToTime = (delegate* unmanaged[Cdecl]<Instance, long*, long*, Result>)performanceToTimePointer.Handle;

        var sessionInfo = new SessionCreateInfo(systemId: SystemId, createFlags: 0, next: &binding);

        xr.CreateSession(Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");

    }

    public void Dispose()
    {
        xr.DestroySession(Session).LogResult("DestroySession", logger);
        xr.DestroyInstance(Instance).LogResult("DestroyInstance", logger);
    }

    private delegate* unmanaged[Cdecl]<Instance, long*, long*, Result> performanceToTime = null;

    public long Now()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long time;
        performanceToTime(Instance, &timestamp, &time).CheckResult("performanceToTime");
        return time;

    }
}