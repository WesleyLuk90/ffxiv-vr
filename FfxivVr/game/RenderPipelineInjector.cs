using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.Interop;

namespace FfxivVR;

public unsafe class RenderPipelineInjector
{
    public static int LeftEyeMarkerTargetCount = 101;
    public static int RightEyeMarkerTargetCount = 102;

    private static Context* GetRenderCommandQueue()
    {
        var threadLocals = ThreadLocals.ThreadLocalInstance();
        return threadLocals == null ? null : threadLocals->GraphicsKernelContext;
    }

    private static void QueueCommand<T>(T command) where T : unmanaged
    {
        var queue = GetRenderCommandQueue();
        if (queue == null)
        {
            return;
        }
        var slot = (T*)queue->AllocateCommand((ulong)sizeof(T));
        if (slot == null)
        {
            return;
        }
        *slot = command;
        queue->PushBackCommand(slot);
    }

    public void QueueEyeMarkerCommand(Eye eye)
    {
        QueueCommand(new RenderCommandSetTarget
        {
            Type = RenderCommandType.SetTarget,
            RenderTargetCount = eye == Eye.Left ? LeftEyeMarkerTargetCount : RightEyeMarkerTargetCount,
        });
    }

    public void QueueClearCommand()
    {
        QueueCommand(new RenderCommandClear
        {
            Type = RenderCommandType.Clear,
            ClearFlags = ClearFlags.Color,
            ClearDepth = 1,
        });
    }
}