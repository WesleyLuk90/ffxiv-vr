using System.Collections.Generic;

namespace FfxivVR;

public interface IEndFrameListener
{
    void EndFrame();
}

public class EndFrameDispatcher
{
    private readonly object listenersLock = new();
    private readonly List<IEndFrameListener> listeners = new();

    public void Register(IEndFrameListener listener)
    {
        lock (listenersLock)
        {
            listeners.Add(listener);
        }
    }

    public void Unregister(IEndFrameListener listener)
    {
        lock (listenersLock)
        {
            listeners.Remove(listener);
        }
    }

    public void EndFrame()
    {
        IEndFrameListener[] snapshot;
        lock (listenersLock)
        {
            snapshot = listeners.ToArray();
        }
        foreach (var listener in snapshot)
        {
            listener.EndFrame();
        }
    }
}