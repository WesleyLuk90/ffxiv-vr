using Silk.NET.Core;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Diagnostics;
using System.Linq;

namespace FfxivVR;
public unsafe class VRSystem : IDisposable
{
    public Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;
    private readonly XR xr;
    private readonly ID3D11Device* device;
    private readonly Logger logger;
    private readonly HookStatus hookStatus;
    private readonly Configuration configuration;
    public Instance Instance = new Instance();
    public ulong SystemId;

    public RuntimeAdjustments RuntimeAdjustments = new RuntimeAdjustments();

    public VRSystem(XR xr, ID3D11Device* device, Logger logger, HookStatus hookStatus, Configuration configuration)
    {
        this.xr = xr;
        this.device = device;
        this.logger = logger;
        this.hookStatus = hookStatus;
        this.configuration = configuration;
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

        var handTrackingExtensionIndex = extensions.FindIndex(p => p.GetExtensionName() == "XR_EXT_hand_tracking");
        HandTrackingExtensionEnabled = handTrackingExtensionIndex > -1;

        byte*[] extensionsToEnable;
        if (HandTrackingExtensionEnabled)
        {
            var handTrackingExtension = extensions[handTrackingExtensionIndex];
            extensionsToEnable = [dx11Extension.ExtensionName, performanceCounterExtension.ExtensionName, handTrackingExtension.ExtensionName];
        }
        else
        {
            extensionsToEnable = [dx11Extension.ExtensionName, performanceCounterExtension.ExtensionName];
        }
        fixed (byte** ptr = &extensionsToEnable[0])
        {
            InstanceCreateInfo createInfo = new InstanceCreateInfo(createFlags: 0, enabledExtensionCount: (uint)extensionsToEnable.Length, enabledExtensionNames: ptr);
            createInfo.ApplicationInfo = appInfo;
            xr.CreateInstance(&createInfo, ref Instance).CheckResult("CreateInstance");
        }

        var instanceProperties = new InstanceProperties(next: null);
        xr.GetInstanceProperties(Instance, &instanceProperties).CheckResult("GetInstanceProperties");

        var runtimeName = instanceProperties.GetRuntimeName();
        logger.Debug($"Runtime Name {runtimeName} Runtime Version {instanceProperties.RuntimeVersion}");
        if (runtimeName == "Oculus")
        {
            logger.Debug("Using OculusRuntimeAdjustments");
            RuntimeAdjustments = new OculusRuntimeAdjustments();
        }
        if (runtimeName.Contains("SteamVR") && !hookStatus.DXGICreateHooked)
        {
            logger.Error("SteamVR requires Dalamud Settings > Wait for plugins before game loads to be enabled. Please enable the setting and restart the game.");
        }

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

        PfnVoidFunction performanceToTimePointer = new PfnVoidFunction();
        xr.GetInstanceProcAddr(Instance, "xrConvertWin32PerformanceCounterToTimeKHR", &performanceToTimePointer).CheckResult("GetInstanceProcAddr");
        performanceToTime = (delegate* unmanaged[Cdecl]<Instance, long*, long*, Result>)performanceToTimePointer.Handle;

        var binding = new GraphicsBindingD3D11KHR(device: device);
        var sessionInfo = new SessionCreateInfo(systemId: SystemId, createFlags: 0, next: &binding);
        xr.CreateSession(Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");

        if (HandTrackingExtensionEnabled)
        {
            CreateHandTracking();
        }
        else if (configuration.HandTracking)
        {
            logger.Info("Hand tracking is not supported by your runtime");
        }
    }

    private void CreateHandTracking()
    {
        var handTrackingProperties = new SystemHandTrackingPropertiesEXT(next: null);
        var systemProperties = new SystemProperties(next: &handTrackingProperties);
        xr.GetSystemProperties(Instance, SystemId, &systemProperties).CheckResult("GetSystemProperties");

        logger.Debug($"Hand tracking enabled {handTrackingProperties.SupportsHandTracking}");

        if (handTrackingProperties.SupportsHandTracking == 1)
        {
            PfnVoidFunction xrCreateHandTrackerEXT = new PfnVoidFunction();
            xr.GetInstanceProcAddr(Instance, "xrCreateHandTrackerEXT", &xrCreateHandTrackerEXT).CheckResult("GetInstanceProcAddr");
            PfnVoidFunction xrDestroyHandTrackerEXT = new PfnVoidFunction();
            xr.GetInstanceProcAddr(Instance, "xrDestroyHandTrackerEXT", &xrDestroyHandTrackerEXT).CheckResult("GetInstanceProcAddr");
            PfnVoidFunction xrLocateHandJointsEXT = new PfnVoidFunction();
            xr.GetInstanceProcAddr(Instance, "xrLocateHandJointsEXT", &xrLocateHandJointsEXT).CheckResult("GetInstanceProcAddr");
            HandTrackerExtension = new HandTrackerExtension(
                xrCreateHandTrackerEXT: xrCreateHandTrackerEXT,
                xrDestroyHandTrackerEXT: xrDestroyHandTrackerEXT,
                xrLocateHandJointsEXT: xrLocateHandJointsEXT
            );
            HandTrackerExtension.Initialize(Session);
        }
        else if (configuration.HandTracking)
        {
            logger.Info("Hand tracking is not supported");
        }
    }

    public void Dispose()
    {
        HandTrackerExtension?.Dispose();
        xr.DestroySession(Session).LogResult("DestroySession", logger);
        xr.DestroyInstance(Instance).LogResult("DestroyInstance", logger);
    }

    private delegate* unmanaged[Cdecl]<Instance, long*, long*, Result> performanceToTime = null;

    public HandTrackerExtension? HandTrackerExtension = null;

    public bool HandTrackingExtensionEnabled { get; private set; }

    public long Now()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long time;
        performanceToTime(Instance, &timestamp, &time).CheckResult("performanceToTime");
        return time;

    }
}