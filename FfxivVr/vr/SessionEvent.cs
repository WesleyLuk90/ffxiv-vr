namespace FfxivVR;

public abstract class SessionEvent
{
    private SessionEvent() { }

    public sealed class Began : SessionEvent
    {
        public static readonly Began Instance = new();
        private Began() { }
    }

    public sealed class Ended : SessionEvent
    {
        public static readonly Ended Instance = new();
        private Ended() { }
    }

    public sealed class Exited(StopReason reason) : SessionEvent
    {
        public StopReason Reason { get; } = reason;
    }
}