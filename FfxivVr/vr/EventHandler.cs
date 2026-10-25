using Silk.NET.OpenXR;
using System;
using System.Runtime.CompilerServices;

namespace FfxivVR;

public class EventHandler(
    XR xr,
    VRSystem vrSystem,
    Logger logger,
    VRSpace vrSpace,
    WaitFrameService waitFrameService,
    VRActionService vrInput,
    IRenderStrategy renderStrategy)
{
    internal unsafe SessionEvent? PollEvents()
    {
        SessionEvent? outcome = null;
        while (true)
        {
            var eventDataBuffer = new EventDataBuffer(next: null);
            var result = xr.PollEvent(vrSystem.Instance, ref eventDataBuffer);
            if (result == Result.EventUnavailable)
            {
                return outcome;
            }
            result.CheckResult("PollEvent");
            switch (eventDataBuffer.Type)
            {
                case StructureType.EventDataEventsLost:
                    {
                        var eventsLost = Unsafe.As<EventDataBuffer, EventDataEventsLost>(ref eventDataBuffer);
                        logger.Error($"Events Lost: {eventsLost.LostEventCount}");
                        break;
                    }
                case StructureType.EventDataInstanceLossPending:
                    {
                        var instanceLoss = Unsafe.As<EventDataBuffer, EventDataInstanceLossPending>(ref eventDataBuffer);
                        logger.Error($"Instance loss pending: {instanceLoss.LossTime}");
                        break;
                    }
                case StructureType.EventDataInteractionProfileChanged:
                    {
                        var interactionProfileChanged = Unsafe.As<EventDataBuffer, EventDataInteractionProfileChanged>(ref eventDataBuffer);
                        logger.Debug($"Interaction profile changed: {interactionProfileChanged.Session}");
                        try
                        {
                            vrInput.InteractionProfileChanged();
                        }
                        catch (Exception e)
                        {
                            logger.Debug($"Failed, ignoring {e} {e.StackTrace}");
                        }
                        break;
                    }
                case StructureType.EventDataReferenceSpaceChangePending:
                    {
                        vrSpace.ResetCamera();
                        break;
                    }
                case StructureType.EventDataSessionStateChanged:
                    {
                        var stateChanged = Unsafe.As<EventDataBuffer, EventDataSessionStateChanged>(ref eventDataBuffer);
                        if (HandleSessionStateChanged(stateChanged) is SessionEvent next && outcome is not SessionEvent.Exited)
                        {
                            outcome = next;
                        }
                        break;
                    }
                default:
                    {
                        logger.Debug($"Unhandled event {eventDataBuffer.Type}");
                        break;
                    }
            }
        }
    }
    private unsafe SessionEvent? HandleSessionStateChanged(EventDataSessionStateChanged stateChanged)
    {
        if (!stateChanged.Session.Equals(vrSystem.Session))
        {
            logger.Error($"Session state changed for different session, got {stateChanged.Session.Handle} but expected {vrSystem.Session.Handle}");
            return null;
        }
        logger.Debug($"Session state has changed to {stateChanged.State}");
        switch (stateChanged.State)
        {
            case SessionState.Ready:
                {
                    var beginInfo = new SessionBeginInfo(next: null, primaryViewConfigurationType: vrSystem.ViewConfigurationType);
                    xr.BeginSession(vrSystem.Session, ref beginInfo).CheckResult("BeginSession");
                    waitFrameService.SessionStarted();
                    logger.Debug("Started session");
                    return SessionEvent.Began.Instance;
                }
            case SessionState.Stopping:
                {
                    waitFrameService.SessionStopped();
                    renderStrategy.OnSessionEnd();
                    xr.EndSession(vrSystem.Session).CheckResult("EndSession");
                    return SessionEvent.Ended.Instance;
                }
            case SessionState.Exiting:
                {
                    return new SessionEvent.Exited(StopReason.RuntimeExiting);
                }
            case SessionState.LossPending:
                {
                    return new SessionEvent.Exited(StopReason.SessionLost);
                }
            default:
                {
                    return null;
                }
        }
    }
}