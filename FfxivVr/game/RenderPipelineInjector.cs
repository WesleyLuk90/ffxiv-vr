using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Memory;

namespace FfxivVR;

public unsafe class RenderPipelineInjector
{
    private delegate void PushbackDg(UInt64 a, UInt64 b);
    [Signature("E8 ?? ?? ?? ?? 0F 28 B4 24 A0 01 00 00 48 8B 8C 24 90 01 00 00", Fallibility = Fallibility.Fallible)]
    private PushbackDg? PushbackFn = null;

    private delegate UInt64 AllocateQueueMemoryDg(UInt64 a, UInt64 b);
    [Signature("E8 ?? ?? ?? ?? 48 8B F8 48 85 C0 0f 84 ?? ?? ?? ?? 45 33 C0 41 BA 05 00 00 00", Fallibility = Fallibility.Fallible)]
    private AllocateQueueMemoryDg? AllocateQueueMemmoryFn = null;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate UInt64 GetThreadedDataDg();
    GetThreadedDataDg GetThreadedDataFn;

    private GCHandle getThreadedDataHandle;
    byte[] GetThreadedDataASM =
        {
                0x55, // push rbp
                0x65, 0x48, 0x8B, 0x04, 0x25, 0x58, 0x00, 0x00, 0x00, // mov rax,gs:[00000058]
                0x5D, // pop rbp
                0xC3  // ret
            };
    private nint tls_index;

    public void Initialize()
    {
        gameInteropProvider.InitializeFromAttributes(this);
    }
    private const string g_tls_index = "8B 0D ?? ?? ?? ?? 45 33 E4 41";
    public RenderPipelineInjector(ISigScanner sigScanner, Logger logger, IGameInteropProvider gameInteropProvider)
    {
        tls_index = sigScanner.GetStaticAddressFromSig(g_tls_index);
        getThreadedDataHandle = GCHandle.Alloc(GetThreadedDataASM, GCHandleType.Pinned);
        var processHandle = PInvoke.GetCurrentProcess_SafeHandle();
        if (!PInvoke.VirtualProtectEx(processHandle, (void*)getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out PAGE_PROTECTION_FLAGS _))
        {
            throw new Exception("Failed to VirtualProtectEx");
        }
        if (!PInvoke.FlushInstructionCache(processHandle, (void*)getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length))
        {
            throw new Exception("Failed to FlushInstructionCache");
        }

        GetThreadedDataFn = Marshal.GetDelegateForFunctionPointer<GetThreadedDataDg>(getThreadedDataHandle.AddrOfPinnedObject());

        this.logger = logger;
        this.gameInteropProvider = gameInteropProvider;
    }

    public UInt64 GetThreadedOffset()
    {
        UInt64 threadedData = GetThreadedDataFn();
        if (threadedData != 0)
        {
            threadedData = *(UInt64*)(threadedData + (UInt64)((*(int*)tls_index) * 8));
            threadedData = *(UInt64*)(threadedData + 0x238);
        }
        return threadedData;
    }

    public static int LeftEyeRenderTargetNumber = 101;
    public static int RightEyeRenderTargetNumber = 102;
    public void QueueRenderTargetCommand(Eye eye)
    {
        UInt64 threadedOffset = GetThreadedOffset();
        if (threadedOffset != 0)
        {
            SetRenderTargetCommand* queueData = (SetRenderTargetCommand*)AllocateQueueMemmoryFn!(threadedOffset, (ulong)sizeof(SetRenderTargetCommand));
            if (queueData != null)
            {
                *queueData = new SetRenderTargetCommand();
                queueData->SwitchType = 0;
                queueData->numRenderTargets = eye == Eye.Left ? LeftEyeRenderTargetNumber : RightEyeRenderTargetNumber;
                queueData->RenderTarget0 = null;
                PushbackFn!(threadedOffset, (ulong)queueData);
            }
        }
    }

    private readonly Logger logger;
    private readonly IGameInteropProvider gameInteropProvider;

    public void QueueClearCommand()
    {
        bool depth = false;
        float r = 0;
        float g = 0;
        float b = 0;
        float a = 0;
        UInt64 threadedOffset = GetThreadedOffset();
        if (threadedOffset != 0)
        {
            UInt64 queueData = AllocateQueueMemmoryFn!(threadedOffset, (ulong)sizeof(ClearCommand));
            if (queueData != 0)
            {
                ClearCommand* cmd = (ClearCommand*)queueData;
                *cmd = new ClearCommand();
                cmd->SwitchType = 4;
                cmd->clearType = ((depth) ? 7 : 1);
                cmd->colorR = r;
                cmd->colorG = g;
                cmd->colorB = b;
                cmd->colorA = a;
                cmd->clearDepth = 1;
                cmd->clearStencil = 0;
                cmd->clearCheck = 0;
                PushbackFn!(threadedOffset, queueData);
            }
        }
    }
}