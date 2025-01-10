using Silk.NET.Core.Native;
using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.EXT;
using Silk.NET.OpenXR.Extensions.FB;
using Silk.NET.OpenXR.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace FfxivVR;
public unsafe class VRSystem(
    XR xr,
    DxDevice device,
    Logger logger,
    HookStatus hookStatus,
    Configuration configuration
) : IDisposable
{
    public Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;

    public Instance Instance = new Instance();
    public ulong SystemId;

    public RuntimeAdjustments RuntimeAdjustments = new RuntimeAdjustments();


    public class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");
    public class MissingDXHook() : Exception("DX Hook was not configured");

    private List<string> wantedExtensions = [
        KhrD3D11Enable.ExtensionName,
        KhrWin32ConvertPerformanceCounterTime.ExtensionName,
        ExtHandTracking.ExtensionName,
        "XR_EXT_palm_pose",
        FBBodyTracking.ExtensionName,
        "XR_EXT_hand_tracking_data_source",
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
            throw new MissingDXHook();
        }

        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);
        var result = xr.GetSystem(Instance, &getInfo, ref SystemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            throw new FormFactorUnavailableException();
        }
        result.CheckResult("GetSystem");
        var d3d11Ext = GetExtension<KhrD3D11Enable>() ?? throw new Exception("Failed to load KhrD3D11Enable extension");

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        d3d11Ext.GetD3D11GraphicsRequirements(Instance, SystemId, &requirements).CheckResult("GetD3D11GraphicsRequirements");
        logger.Debug($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");

        perfCounterExt = GetExtension<KhrWin32ConvertPerformanceCounterTime>() ?? throw new Exception("Failed to load KhrWin32ConvertPerformanceCounterTime extension"); ;

        var binding = new GraphicsBindingD3D11KHR(device: device.Device);
        var sessionInfo = new SessionCreateInfo(systemId: SystemId, createFlags: 0, next: &binding);
        xr.CreateSession(Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");

        CreateExtensions(foundExtensions);
    }

    private void CreateExtensions(List<ExtensionProperties> foundExtensions)
    {
        if (foundExtensions.Any(e => e.GetExtensionName() == ExtHandTracking.ExtensionName))
        {
            CreateHandTracking();
        }
        if (configuration.HandTracking && HandTracker == null)
        {
            logger.Info("Hand tracking is not supported by your runtime");
        }

        if (foundExtensions.Any(e => e.GetExtensionName() == FBBodyTracking.ExtensionName))
        {
            CreateBodyTracking();
        }
        if (configuration.BodyTracking && BodyTracker == null)
        {
            logger.Info("Body tracking is not supported by your runtime");
        }

    }

    private void CreateBodyTracking()
    {
        var properties = new SystemBodyTrackingPropertiesFB(next: null);
        var systemProperties = new SystemProperties(next: &properties);
        xr.GetSystemProperties(Instance, SystemId, &systemProperties).CheckResult("GetSystemProperties");
        if (properties.SupportsBodyTracking == 1 && GetExtension<FBBodyTracking>() is FBBodyTracking ext)
        {
            logger.Debug("Initializing BodyTracking");
            BodyTracker = new BodyTracking(ext, logger);
            BodyTracker.Initialize(Session);
        }
    }

    private void CreateHandTracking()
    {
        var properties = new SystemHandTrackingPropertiesEXT(next: null);
        var systemProperties = new SystemProperties(next: &properties);
        xr.GetSystemProperties(Instance, SystemId, &systemProperties).CheckResult("GetSystemProperties");

        if (properties.SupportsHandTracking == 1 && GetExtension<ExtHandTracking>() is ExtHandTracking ext)
        {
            logger.Debug("Initializing HandTracking");
            HandTracker = new HandTracking(ext);
            HandTracker.Initialize(Session);
        }
    }

    private T? GetExtension<T>() where T : NativeExtension<XR>
    {
        if (!xr.TryGetInstanceExtension<T>(null, Instance, out T handTracking))
        {
            return null;
        }
        return handTracking;
    }

    public void Dispose()
    {
        HandTracker?.Dispose();
        BodyTracker?.Dispose();
        xr.DestroySession(Session).LogResult("DestroySession", logger);
        xr.DestroyInstance(Instance).LogResult("DestroyInstance", logger);
    }

    private KhrWin32ConvertPerformanceCounterTime perfCounterExt = null!;
    public HandTracking? HandTracker { get; private set; } = null;

    public BodyTracking? BodyTracker { get; private set; } = null;

    public long Now()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long time;
        perfCounterExt.ConvertWin32PerformanceCounterToTime(Instance, &timestamp, &time).CheckResult("ConvertTimeToWin32PerformanceCounter");
        return time;

    }
}