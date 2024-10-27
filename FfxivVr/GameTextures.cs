using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace FfxivVR;
unsafe public static class GameTextures
{
    public static Texture* GetGameRenderTexture()
    {
        return FFXIVClientStructs.FFXIV.Client.Graphics.Render.RenderTargetManager.Instance()->RenderTargets2[33].Value;
    }
}
