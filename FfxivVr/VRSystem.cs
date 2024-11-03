using Silk.NET.Core;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Diagnostics;

namespace FfxivVR;
public unsafe class VRSystem : IDisposable
{
    public Session Session = new Session();
    internal ViewConfigurationType ViewConfigurationType = ViewConfigurationType.PrimaryStereo;
    private readonly XR xr;
    private readonly ID3D11Device* device;
    private readonly Logger logger;
    private readonly HookStatus hookStatus;
    private readonly VRInstance vrInstance;

    public VRSystem(XR xr, ID3D11Device* device, Logger logger, HookStatus hookStatus, VRInstance vrInstance)
    {
        this.xr = xr;
        this.device = device;
        this.logger = logger;
        this.hookStatus = hookStatus;
        this.vrInstance = vrInstance;
    }

    public class FormFactorUnavailableException() : Exception("Form factor unavailable, make sure the headset is connected");

    public void Initialize()
    {
        if (!vrInstance.IsVRAvailable())
        {
            logger.Error("Headset not found");
            throw new FormFactorUnavailableException();
        }
        PfnVoidFunction getRequirementsPointer = new PfnVoidFunction();
        xr.GetInstanceProcAddr(vrInstance.Instance, "xrGetD3D11GraphicsRequirementsKHR", &getRequirementsPointer).CheckResult("GetInstanceProcAddr");
        var getRequirements = (delegate* unmanaged[Cdecl]<Instance, ulong, GraphicsRequirementsD3D11KHR*, Result>)getRequirementsPointer.Handle;

        GraphicsRequirementsD3D11KHR requirements = new GraphicsRequirementsD3D11KHR(next: null);
        getRequirements(vrInstance.Instance, vrInstance.SystemId, &requirements).CheckResult("xrGetD3D11GraphicsRequirementsKHR");
        logger.Debug($"Requirements Adapter {requirements.AdapterLuid} Feature level {requirements.MinFeatureLevel}");

        var binding = new GraphicsBindingD3D11KHR(device: device);

        PfnVoidFunction performanceToTimePointer = new PfnVoidFunction();
        xr.GetInstanceProcAddr(vrInstance.Instance, "xrConvertWin32PerformanceCounterToTimeKHR", &performanceToTimePointer).CheckResult("GetInstanceProcAddr");
        performanceToTime = (delegate* unmanaged[Cdecl]<Instance, long*, long*, Result>)performanceToTimePointer.Handle;

        var sessionInfo = new SessionCreateInfo(systemId: vrInstance.SystemId, createFlags: 0, next: &binding);
        xr.CreateSession(vrInstance.Instance, ref sessionInfo, ref Session).CheckResult("CreateSession");

    }

    public void Dispose()
    {
        xr.DestroySession(Session).LogResult("DestroySession", logger);
    }

    private delegate* unmanaged[Cdecl]<Instance, long*, long*, Result> performanceToTime = null;

    public long Now()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long time;
        performanceToTime(vrInstance.Instance, &timestamp, &time).CheckResult("performanceToTime");
        return time;

    }
}