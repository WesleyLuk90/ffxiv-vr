
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Direct3D11;
using System.Collections.Generic;

namespace FfxivVR;


public unsafe class RenderTargetLookup
{
    private Dictionary<nint, nint> renderTargetsToTextures = new Dictionary<nint, nint>();

    public bool IsRenderTexture(ID3D11RenderTargetView* rtv)
    {
        var pointer = new nint(rtv);
        if (!renderTargetsToTextures.ContainsKey(pointer))
        {
            var texture = lookupTexture(rtv);
            renderTargetsToTextures.Add(pointer, texture);
            return IsMainTexture(texture);
        }
        return IsMainTexture(renderTargetsToTextures.GetValueOrDefault(pointer));
    }

    public bool IsDepthStencil(ID3D11DepthStencilView* dsv)
    {
        var pointer = new nint(dsv);
        if (!renderTargetsToTextures.ContainsKey(pointer))
        {
            var texture = lookupDepthStencil(dsv);
            renderTargetsToTextures.Add(pointer, texture);
            return IsDepthStencil(texture);
        }
        return IsDepthStencil(renderTargetsToTextures.GetValueOrDefault(pointer));
    }

    private nint lookupTexture(ID3D11RenderTargetView* rtv)
    {
        ID3D11Resource* resource;
        rtv->GetResource(&resource);
        return new nint(resource);
    }

    private nint lookupDepthStencil(ID3D11DepthStencilView* dsv)
    {
        ID3D11Resource* resource;
        dsv->GetResource(&resource);
        return new nint(resource);
    }

    private bool IsMainTexture(nint tex2d)
    {
        var renderTargetManager = RenderTargetManager.Instance();
        if (renderTargetManager == null)
        {
            return false;
        }
        var texture = renderTargetManager->ToneAdjustSource;
        if (texture == null)
        {
            return false;
        }
        return tex2d.ToPointer() == texture->D3D11Texture2D;
    }
    private bool IsDepthStencil(nint tex2d)
    {
        var renderTargetManager = RenderTargetManager.Instance();
        if (renderTargetManager == null)
        {
            return false;
        }
        var texture = renderTargetManager->DepthStencil;
        if (texture == null)
        {
            return false;
        }
        return tex2d.ToPointer() == texture->D3D11Texture2D;
    }
}