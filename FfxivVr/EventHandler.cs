using Lumina;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.STD.Helper.IStaticEncoding;

namespace FfxivVR
{
    internal class EventHandler
    {
        private XR xr;
        private VRSystem vrSystem;
        private Logger logger;
        private VRState vrState;

        internal EventHandler(XR xr, VRSystem vrSystem, Logger logger, VRState vrState)
        {
            this.xr = xr;
            this.vrSystem = vrSystem;
            this.logger = logger;
            this.vrState = vrState;
        }
        internal unsafe void PollEvents()
        {
            var eventDataBuffer = new EventDataBuffer(next: null);
            while (true)
            {
                var result = xr.PollEvent(vrSystem.Instance, ref eventDataBuffer);
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
                            logger.Debug($"Space change: {spaceChangePending.Session}");
                            break;
                        }
                    case StructureType.EventDataSessionStateChanged:
                        {
                            var stateChanged = Unsafe.As<EventDataBuffer, EventDataSessionStateChanged>(ref eventDataBuffer);
                            HandleSessionStateChanged(stateChanged);
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

        private unsafe void HandleSessionStateChanged(EventDataSessionStateChanged stateChanged)
        {
            if (stateChanged.Session.Equals(vrSystem.Session))
            {
                logger.Error($"Session state changed for different session");
                return;
            }
            switch (stateChanged.State)
            {
                case SessionState.Ready:
                    {
                        var beginInfo = new SessionBeginInfo(next: null, primaryViewConfigurationType: vrSystem.ViewConfigurationType);
                        xr.BeginSession(vrSystem.Session, ref beginInfo).CheckResult("BeginSession");
                        vrState.SessionRunning = true;
                        break;
                    }
                case SessionState.Stopping:
                    {
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
