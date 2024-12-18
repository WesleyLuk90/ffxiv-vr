using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
unsafe struct InternalRenderTargetManager
{
    public static InternalRenderTargetManager* Instance() => (InternalRenderTargetManager*)RenderTargetManager.Instance();

    [FieldOffset(624 + 32 * 8)]
    public unsafe Texture* DrawTexture;
    [FieldOffset(32 + 10 * 8)]
    public unsafe Texture* DepthStencilTexture;

}