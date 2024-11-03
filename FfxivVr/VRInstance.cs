using Silk.NET.OpenXR;
using System;
using System.Linq;

namespace FfxivVR;
public unsafe class VRInstance(XR xr, Logger logger, HookStatus hookStatus) : IDisposable
{
    public Instance Instance = new Instance();
    public ulong SystemId;

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

        var runtimeName = instanceProperties.GetRuntimeName();
        logger.Debug($"Runtime Name {instanceProperties.GetRuntimeName()} Runtime Version {instanceProperties.RuntimeVersion}");
        if (runtimeName.Contains("SteamVR") && !hookStatus.DXGICreateHooked)
        {
            logger.Error("SteamVR requires Dalamud Settings > Wait for plugins before game loads to be enabled. Please enable the setting and restart the game.");
        }
    }

    public bool IsVRAvailable()
    {
        var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);
        var result = xr.GetSystem(Instance, &getInfo, ref SystemId);
        if (result == Result.ErrorFormFactorUnavailable)
        {
            return false;
        }
        result.CheckResult("GetSystem");
        return true;
    }

    public void Dispose()
    {
        xr.DestroyInstance(Instance).LogResult("DestroyInstance", logger);
    }
}
