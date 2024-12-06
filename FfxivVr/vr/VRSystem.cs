using Silk.NET.Core;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace FfxivVR;
public unsafe class VRSystem : IDisposable
{
    public Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;
    private readonly XR xr;
    private readonly DxDevice device;
    private readonly Logger logger;
    private readonly HookStatus hookStatus;
    private readonly Configuration configuration;
    public Instance Instance = new Instance();
    public ulong SystemId;

    public RuntimeAdjustments RuntimeAdjustments = new RuntimeAdjustments();

    public VRSystem(XR xr,
        DxDevice device, Logger logger, HookStatus hookStatus, Configuration configuration)
    {
        this.xr = xr;
        this.device = device;
        this.logger = logger;
        this.hookStatus = hookStatus;
        this.configuration = configuration;
    }

    public class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");

    private List<string> wantedExtensions = [
        "XR_KHR_D3D11_enable",
        "XR_KHR_win32_convert_performance_counter_time",
        "XR_EXT_hand_tracking",
        "XR_EXT_palm_pose",
    ];
    public void Initialize()
    {
        ApplicationInfo appInfo = new ApplicationInfo(applicationVersion: 1, engineVersion: 1, apiVersion: 1UL << 48);
        appInfo.SetApplicationName("FFXIV VR");

        var availableExtensions = xr.GetInstanceExtensionProperties(layerName: null);
        var names = string.Join(",", availableExtensions.Select(e => e.GetExtensionName()).ToList());

        logger.Debug($"Available extensions ({availableExtensions.Count}): {names}");

        var foundExtensions = new List<ExtensionProperties>();
        wantedExtensions.ForEach(wantedExtension =>
        {
            availableExtensions.ForEach(available =>
            {
                if (available.GetExtensionName() == wantedExtension)
                {
                    foundExtensions.Add(available);
                }
            });
        });
        logger.Debug($"Enabling extensions {string.Join(", ", foundExtensions.Select(e => e.GetExtensionName()))}");

        HandTrackingExtensionEnabled = foundExtensions.Any(e => e.GetExtensionName() == "XR_EXT_hand_tracking");
        byte*[] extensionsToEnable = new byte*[foundExtensions.Count()];
        for (var i = 0; i < foundExtensions.Count(); i++)
        {
            extensionsToEnable[i] = (byte*)Marshal.StringToHGlobalAnsi(foundExtensions[i].GetExtensionName());
        }
        fixed (byte** ptr = &extensionsToEnable[0])
        {
            InstanceCreateInfo createInfo = new InstanceCreateInfo(
                createFlags: 0,
                enabledExtensionCount: (uint)extensionsToEnable.Length,
                enabledExtensionNames: ptr);
            createInfo.ApplicationInfo = appInfo;
            xr.CreateInstance(&createInfo, ref Instance).CheckResult("CreateInstance");
        }
        foreach (var stringPointer in extensionsToEnable)
        {
            Marshal.FreeHGlobal((IntPtr)stringPointer);
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
        if (runtimeName.Contains("SteamVR") && !hookStatus.IsHookAdded())
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

        var binding = new GraphicsBindingD3D11KHR(device: device.Device);
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
            HandTrackerExtension = new HandTracking(
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

    public HandTracking? HandTrackerExtension = null;

    public bool HandTrackingExtensionEnabled { get; private set; }

    public long Now()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long time;
        performanceToTime(Instance, &timestamp, &time).CheckResult("performanceToTime");
        return time;

    }
}