using Dalamud.Game;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FfxivVR;
public unsafe class RenderPipelineInjector
{
    private delegate void PushbackDg(UInt64 a, UInt64 b);
    [Signature(Signatures.Pushback, Fallibility = Fallibility.Fallible)]
    private PushbackDg? PushbackFn = null;

    private delegate UInt64 AllocateQueueMemoryDg(UInt64 a, UInt64 b);
    [Signature(Signatures.AllocateQueueMemory, Fallibility = Fallibility.Fallible)]
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

    [DllImport("kernel32.dll")]
    static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
    [DllImport("kernel32.dll")]
    static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);
    public RenderPipelineInjector(ISigScanner sigScanner, Logger logger)
    {
        tls_index = sigScanner.GetStaticAddressFromSig(Signatures.g_tls_index);
        getThreadedDataHandle = GCHandle.Alloc(GetThreadedDataASM, GCHandleType.Pinned);
        if (!VirtualProtectEx(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length, 0x40 /* EXECUTE_READWRITE */, out uint _))
        {
            throw new Exception("Failed to VirtualProtectEx");
        }
        if (!FlushInstructionCache(Process.GetCurrentProcess().Handle, getThreadedDataHandle.AddrOfPinnedObject(), (UIntPtr)GetThreadedDataASM.Length))
        {
            throw new Exception("Failed to FlushInstructionCache");
        }

        GetThreadedDataFn = Marshal.GetDelegateForFunctionPointer<GetThreadedDataDg>(getThreadedDataHandle.AddrOfPinnedObject());

        this.logger = logger;
    }

    public UInt64 GetThreadedOffset()
    {
        UInt64 threadedData = GetThreadedDataFn();
        if (threadedData != 0)
        {
            threadedData = *(UInt64*)(threadedData + (UInt64)((*(int*)tls_index) * 8));
            threadedData = *(UInt64*)(threadedData + 0x250);
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

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct SetRenderTargetCommand
    {
        [FieldOffset(0x00)] public int SwitchType;
        [FieldOffset(0x04)] public int numRenderTargets;
        [FieldOffset(0x08)] public Texture* RenderTarget0;
        [FieldOffset(0x10)] public Texture* RenderTarget1;
        [FieldOffset(0x18)] public Texture* RenderTarget2;
        [FieldOffset(0x20)] public Texture* RenderTarget3;
        [FieldOffset(0x28)] public Texture* RenderTarget4;
        [FieldOffset(0x30)] public Texture* DepthBuffer;
        [FieldOffset(0x38)] public short unk3;
        [FieldOffset(0x38)] public short unk4;
        [FieldOffset(0x38)] public short unk5;
        [FieldOffset(0x38)] public short unk6;
    };

    private readonly Logger logger;

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

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ClearCommand
{
    [FieldOffset(0x00)] public int SwitchType;
    [FieldOffset(0x04)] public int clearType;
    [FieldOffset(0x08)] public float colorB;
    [FieldOffset(0x0C)] public float colorG;
    [FieldOffset(0x10)] public float colorR;
    [FieldOffset(0x14)] public float colorA;
    [FieldOffset(0x18)] public float clearDepth;
    [FieldOffset(0x1C)] public int clearStencil;
    [FieldOffset(0x20)] public int clearCheck;
    [FieldOffset(0x24)] public float Top;
    [FieldOffset(0x28)] public float Left;
    [FieldOffset(0x2C)] public float Width;
    [FieldOffset(0x30)] public float Height;
    [FieldOffset(0x34)] public float MinZ;
    [FieldOffset(0x38)] public float MaxZ;
};