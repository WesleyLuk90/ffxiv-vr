using Silk.NET.OpenXR;
using System.Runtime.CompilerServices;

namespace FfxivVR
{
    internal class EventHandler
    {
        private readonly XR xr;
        private readonly VRSystem vrSystem;
        private readonly Logger logger;
        private readonly VRState vrState;
        private readonly VRSpace vrSpace;
        private readonly WaitFrameService waitFrameService;
        private readonly VRInstance vrInstance;

        internal EventHandler(XR xr, VRSystem vrSystem, Logger logger, VRState vrState, VRSpace vrSpace, WaitFrameService waitFrameService, VRInstance vrInstance)
        {
            this.xr = xr;
            this.vrSystem = vrSystem;
            this.logger = logger;
            this.vrState = vrState;
            this.vrSpace = vrSpace;
            this.waitFrameService = waitFrameService;
            this.vrInstance = vrInstance;
        }
        internal unsafe void PollEvents(System.Action onSessionEnd)
        {
            while (true)
            {
                var eventDataBuffer = new EventDataBuffer(next: null);
                var result = xr.PollEvent(vrInstance.Instance, ref eventDataBuffer);
                if (result == Result.EventUnavailable)
                {
                    return;
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
                            logger.Info($"Interaction profile changed: {interactionProfileChanged.Session}");
                            break;
                        }
                    case StructureType.EventDataReferenceSpaceChangePending:
                        {
                            var spaceChangePending = Unsafe.As<EventDataBuffer, EventDataReferenceSpaceChangePending>(ref eventDataBuffer);
                            vrSpace.ResetCamera();
                            break;
                        }
                    case StructureType.EventDataSessionStateChanged:
                        {
                            var stateChanged = Unsafe.As<EventDataBuffer, EventDataSessionStateChanged>(ref eventDataBuffer);
                            HandleSessionStateChanged(stateChanged, onSessionEnd);
                            break;
                        }
                    default:
                        {
                            logger.Error($"Unhandled event {eventDataBuffer.Type}");
                            break;
                        }
                }
            }
        }

        private unsafe void HandleSessionStateChanged(EventDataSessionStateChanged stateChanged, System.Action onSessionEnd)
        {
            if (!stateChanged.Session.Equals(vrSystem.Session))
            {
                logger.Error($"Session state changed for different session, got {stateChanged.Session.Handle} but expected {vrSystem.Session.Handle}");
                return;
            }
            logger.Debug($"Session state has changed to {stateChanged.State}");
            switch (stateChanged.State)
            {
                case SessionState.Ready:
                    {
                        var beginInfo = new SessionBeginInfo(next: null, primaryViewConfigurationType: vrSystem.ViewConfigurationType);
                        xr.BeginSession(vrSystem.Session, ref beginInfo).CheckResult("BeginSession");
                        waitFrameService.SessionStarted();
                        vrState.SessionRunning = true;
                        logger.Debug("Started session");
                        break;
                    }
                case SessionState.Stopping:
                    {
                        waitFrameService.SessionStopped();
                        onSessionEnd();
                        xr.EndSession(vrSystem.Session).CheckResult("EndSession");
                        vrState.SessionRunning = false;
                        break;
                    }
                case SessionState.Exiting:
                    {
                        vrState.SessionRunning = false;
                        vrState.Exiting = true;
                        break;
                    }
                case SessionState.LossPending:
                    {
                        vrState.SessionRunning = false;
                        vrState.Exiting = true;
                        break;
                    }
            }
            vrState.State = stateChanged.State;
        }
    }
}
