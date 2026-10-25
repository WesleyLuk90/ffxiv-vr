using Microsoft.Extensions.DependencyInjection;

namespace FfxivVR;

public abstract class VRStatus
{
    private VRStatus() { }

    public sealed class Off : VRStatus
    {
        public static readonly Off Instance = new();
        private Off() { }
    }

    public sealed class RestartPending : VRStatus
    {
        public static readonly RestartPending Instance = new();
        private RestartPending() { }
    }

    public abstract class Active : VRStatus
    {
        private protected Active(IServiceScope scope, VRSession session)
        {
            Scope = scope;
            Session = session;
        }

        public IServiceScope Scope { get; }
        public VRSession Session { get; }
    }

    public sealed class Idle(IServiceScope scope, VRSession session) : Active(scope, session)
    {
    }

    public sealed class Running(IServiceScope scope, VRSession session) : Active(scope, session)
    {
    }

    public sealed class Stopping(IServiceScope scope, VRSession session, StopReason reason) : Active(scope, session)
    {
        public StopReason Reason { get; } = reason;
    }
}

public enum StopReason
{
    UserRequested,
    Restart,
    RuntimeExiting,
    SessionLost,
    Exception,
    FatalError,
    PluginUnloaded,
}