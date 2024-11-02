using Silk.NET.OpenXR;
using System;
using System.Threading;

namespace FfxivVR;
public unsafe class WaitFrameService(VRSystem system, XR xr)
{
    private readonly VRSystem system = system;
    private readonly XR xr = xr;
    private Mutex mutex = new Mutex();
    private bool sessionStatus = false;
    public void SessionStarted()
    {
        mutex.WaitOne();
        sessionStatus = true;
        mutex.ReleaseMutex();
    }
    public void SessionStopped()
    {
        // Ensure that we never call WaitFrame once we call EndFrame
        mutex.WaitOne();
        sessionStatus = false;
        mutex.ReleaseMutex();
    }
    internal FrameState WaitFrame()
    {
        var frameState = new FrameState(next: null);
        mutex.WaitOne();
        try
        {
            if (!sessionStatus)
            {
                throw new Exception("Session has ended");
            }
            xr.WaitFrame(system.Session, null, ref frameState).CheckResult("WaitFrame");
        }
        finally { mutex.ReleaseMutex(); }
        return frameState;
    }
}
