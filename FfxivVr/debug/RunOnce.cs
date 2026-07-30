using System;
using System.Collections.Generic;
using System.Threading;

namespace FfxivVR;

enum State
{
    Idle,
    Waiting,
    Running,
}


public class RunOnce(
    Logger logger
)
{
    private Mutex mutex = new();
    private Dictionary<string, HashSet<string>> keys = new();
    private State state = State.Idle;
    public void StartFrameAction()
    {
        withMutex(() =>
        {
            if (state == State.Idle)
            {
                state = State.Waiting;
                logger.Info("StartFrameAction");
            }
        });
    }

    public void EndFrame()
    {
        withMutex(() =>
        {
            if (state == State.Waiting)
            {
                keys.Clear();
                state = State.Running;
                logger.Info("RunFrameAction");
            }
            else if (state == State.Running)
            {
                state = State.Idle;
                logger.Info("EndFrameAction");
            }
        });
    }

    public bool IsRunningAction()
    {
        return state == State.Running;
    }
    public void Run(string scope, string key, Action action)
    {
        if (!IsRunningAction())
        {
            return;
        }
        var shouldRun = withMutex(() =>
         {
             if (!keys.ContainsKey(scope))
             {
                 keys[scope] = new();
             }
             var shouldRun = keys[scope].Contains(key);
             keys[scope].Add(key);
             return shouldRun;
         });
        if (shouldRun)
        {
            action();
        }
    }

    private T withMutex<T>(Func<T> action)
    {
        mutex.WaitOne();
        try
        {
            return action();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
    private void withMutex(Action action)
    {
        mutex.WaitOne();
        try
        {
            action();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}