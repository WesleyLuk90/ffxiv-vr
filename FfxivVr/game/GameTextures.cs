﻿using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;

namespace FfxivVR;

public unsafe class Cursor(
    bool visible,
    Vector2D<float> uvScale,
    Vector2D<float> uvOffset,
    ID3D11ShaderResourceView* shaderResourceView
)
{
    public bool Visible { get; } = visible;
    public Vector2D<float> UvScale { get; } = uvScale;
    public Vector2D<float> UvOffset { get; } = uvOffset;
    public ID3D11ShaderResourceView* ShaderResourceView { get; } = shaderResourceView;

    public float Width = 64;
    public float Height = 64;
}
public static unsafe class GameTextures
{
    public static Texture* GetGameRenderTexture()
    {
        return InternalRenderTargetManager.Instance()->DrawTexture;
    }
    public static Texture* GetGameDepthTexture()
    {
        return InternalRenderTargetManager.Instance()->DepthStencilTexture;
    }

    internal static Cursor? GetCursorTexture()
    {
        var framework = Framework.Instance();
        if (framework == null)
        {
            return null;
        }
        var cursor = framework->Cursor;
        if (cursor == null)
        {
            return null;
        }
        var resourceHandle = cursor->SoftwareCursorTexture;
        if (resourceHandle == null)
        {
            return null;
        }
        var texture = resourceHandle->Texture;
        if (texture == null)
        {
            return null;
        }
        // common/softwarecursor/Cursor_64px.tex
        // Default to the pointer
        Vector2D<float> offset = new Vector2D<float>(1, 0);
        switch (cursor->ActiveCursorType)
        {
            // 0 is pointer
            // 6 is hand
            // 11 is hand finger
            // 14 is hand grab
            case 6:
                offset = new Vector2D<float>(2, 0);
                break;
            case 11:
                offset = new Vector2D<float>(0, 1);
                break;
            case 14:
                offset = new Vector2D<float>(3, 0);
                break;
        }
        offset /= new Vector2D<float>(4, 5);
        var scale = new Vector2D<float>(1f / 4, 1f / 5);

        return new Cursor(
            visible: cursor->IsCursorVisible,
            uvScale: scale,
            uvOffset: offset,
            shaderResourceView: (ID3D11ShaderResourceView*)texture->D3D11ShaderResourceView
        );
    }
}