using Dalamud.Configuration;
using System;

namespace FfxivVR;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public float WorldScale = 1.0f;
    public float UIDistance = 1.0f;
    public bool FollowCharacter = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
