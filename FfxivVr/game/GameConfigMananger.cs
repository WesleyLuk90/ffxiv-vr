using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public class GameOption
{
    SystemConfigOption? systemConfigOption;

    public GameOption(SystemConfigOption systemConfigOption, IGameConfig gameConfig)
    {
        this.systemConfigOption = systemConfigOption;
        this.gameConfig = gameConfig;
        if (!gameConfig.TryGet(systemConfigOption, out UIntConfigProperties? maybeProperties) || maybeProperties is not UIntConfigProperties prop)
        {
            throw new Exception($"Failed to get properties for {systemConfigOption}");
        }
        Properties = prop;
    }

    UiConfigOption? uiConfigOption;

    public GameOption(UiConfigOption uiConfigOption, IGameConfig gameConfig)
    {
        this.uiConfigOption = uiConfigOption;
        this.gameConfig = gameConfig;
        if (!gameConfig.TryGet(uiConfigOption, out UIntConfigProperties? maybeProperties) || maybeProperties is not UIntConfigProperties prop)
        {
            throw new Exception($"Failed to get properties for {uiConfigOption}");
        }
        Properties = prop;
    }

    UiControlOption? uiControlOption;
    private readonly IGameConfig gameConfig;

    public GameOption(UiControlOption uiControlOption, IGameConfig gameConfig)
    {
        this.uiControlOption = uiControlOption;
        this.gameConfig = gameConfig;
        if (!gameConfig.TryGet(uiControlOption, out UIntConfigProperties? maybeProperties) || maybeProperties is not UIntConfigProperties prop)
        {
            throw new Exception($"Failed to get properties for {uiControlOption}");
        }
        Properties = prop;
    }

    public UIntConfigProperties Properties;
    public uint GetCurrentValue()
    {
        if (systemConfigOption is SystemConfigOption sys)
        {
            if (gameConfig.TryGet(sys, out uint val))
            {
                return val;
            }
            throw new Exception($"Failed to get config {sys}");
        }
        if (uiControlOption is UiControlOption control)
        {
            if (gameConfig.TryGet(control, out uint val))
            {
                return val;
            }
            throw new Exception($"Failed to get config {control}");
        }
        if (uiConfigOption is UiConfigOption uiConfig)
        {
            if (gameConfig.TryGet(uiConfig, out uint val))
            {
                return val;
            }
            throw new Exception($"Failed to get config {uiConfig}");
        }
        throw new Exception("Missing option");
    }

    public string? Label = null;
    public List<string>? Options;
    public GameOption Initialize(string label, List<string>? options = null)
    {
        Label = label;
        Options = options;
        if (Options is List<string> o)
        {
            if (Properties.Minimum != 0 || Properties.Maximum + 1 != o.Count)
            {
                throw new Exception($"Options mismatch {GetID()} {Properties}");
            }
        }
        return this;
    }


    public string GetID()
    {
        return ToString();
    }
    public override string ToString()
    {
        if (systemConfigOption is SystemConfigOption sys)
        {
            return sys.ToString();
        }
        if (uiControlOption is UiControlOption control)
        {
            return control.ToString();
        }
        if (uiConfigOption is UiConfigOption uiConfig)
        {
            return uiConfig.ToString();
        }
        throw new Exception("Missing option");
    }

    public bool IsBoolean()
    {
        return Options == null && Properties.Minimum == 0 && Properties.Maximum == 1;
    }

    internal void SetValue(uint value)
    {
        if (systemConfigOption is SystemConfigOption sys)
        {
            gameConfig.Set(sys, value);
        }
        else if (uiControlOption is UiControlOption control)
        {
            gameConfig.Set(control, value);
        }
        else if (uiConfigOption is UiConfigOption uiConfig)
        {
            gameConfig.Set(uiConfig, value);
        }
        else
        {
            throw new Exception("Missing option");
        }
    }
}

public class GameConfigManager(
    IGameConfig gameConfig,
    Logger logger,
    Configuration configuration)
{
    private readonly IGameConfig gameConfig = gameConfig;
    private readonly Logger logger = logger;
    private readonly Configuration configuration = configuration;
    public readonly List<GameOption> Options = new();

    public void Initialize()
    {
        if (Options.Count > 0)
        {
            return;
        }
        // Display Settings
        Options.Add(new GameOption(SystemConfigOption.CharaLight, gameConfig).Initialize("Character Lighting"));
        Options.Add(new GameOption(SystemConfigOption.Fps, gameConfig).Initialize("Frame Rate", ["None", "Main Display Refresh Rate", "60 fps", "30 fps"]));

        // Graphics Settings
        Options.Add(new GameOption(SystemConfigOption.GraphicsRezoScale, gameConfig).Initialize("Resolution Scale"));
        Options.Add(new GameOption(SystemConfigOption.DynamicRezoType, gameConfig).Initialize("Enable Dynamic Resolution"));
        Options.Add(new GameOption(SystemConfigOption.DynamicRezoThreshold, gameConfig).Initialize("Framerate Threshold", ["Always Enabled", "Below 30 fps", "Below 60 fps"]));

        Options.Add(new GameOption(SystemConfigOption.ReflectionType_DX11, gameConfig).Initialize("Real-time Reflections", ["Off", "Standard", "High", "Maximum"]));
        Options.Add(new GameOption(SystemConfigOption.GrassQuality_DX11, gameConfig).Initialize("Grass Quality", ["Off", "Low", "Normal", "High"]));
        Options.Add(new GameOption(SystemConfigOption.ParallaxOcclusion_DX11, gameConfig).Initialize("Parallax Occlusion", ["Normal", "High"]));

        Options.Add(new GameOption(SystemConfigOption.ShadowVisibilityTypeSelf_DX11, gameConfig).Initialize("Shadows - Self", ["Hide", "Display"]));
        Options.Add(new GameOption(SystemConfigOption.ShadowVisibilityTypeParty_DX11, gameConfig).Initialize("Shadows - Party Members", ["Hide", "Display"]));
        Options.Add(new GameOption(SystemConfigOption.ShadowVisibilityTypeOther_DX11, gameConfig).Initialize("Shadows - Other NPCs", ["Hide", "Display"]));
        Options.Add(new GameOption(SystemConfigOption.ShadowVisibilityTypeEnemy_DX11, gameConfig).Initialize("Shadows - Enemies", ["Hide", "Display"]));
        Options.Add(new GameOption(SystemConfigOption.ShadowLOD_DX11, gameConfig).Initialize("Shadow LOD"));
        Options.Add(new GameOption(SystemConfigOption.ShadowBgLOD, gameConfig).Initialize("Shadow LOD Distant"));

        Options.Add(new GameOption(SystemConfigOption.TextureAnisotropicQuality_DX11, gameConfig).Initialize("Anisotropic Filtering", ["x4", "x8", "x16"]));

        Options.Add(new GameOption(SystemConfigOption.PhysicsTypeSelf_DX11, gameConfig).Initialize("Movement Physics - Self", ["Off", "Simple", "Full"]));
        Options.Add(new GameOption(SystemConfigOption.PhysicsTypeParty_DX11, gameConfig).Initialize("Movement Physics - Party Members", ["Off", "Simple", "Full"]));
        Options.Add(new GameOption(SystemConfigOption.PhysicsTypeOther_DX11, gameConfig).Initialize("Movement Physics - Other NPCs", ["Off", "Simple", "Full"]));
        Options.Add(new GameOption(SystemConfigOption.PhysicsTypeEnemy_DX11, gameConfig).Initialize("Movement Physics - Enemies", ["Off", "Simple", "Full"]));

        Options.Add(new GameOption(SystemConfigOption.Vignetting_DX11, gameConfig).Initialize("Naturally darken edge of screen"));
        Options.Add(new GameOption(SystemConfigOption.SSAO_DX11, gameConfig).Initialize("Screen Space Ambient Occlusion", ["Off", "Light", "Strong", "HBAO+: Standard", "HBAO+: Quality", "GTAO: Standard", "GTAO: Quality"]));

        // Mouse Settings
        Options.Add(new GameOption(SystemConfigOption.MouseOpeLimit, gameConfig).Initialize("Limit mouse operation to game window."));

        // Other Settings
        Options.Add(new GameOption(SystemConfigOption.DisplayObjectLimitType, gameConfig).Initialize("Display Limits", ["Maximum", "High", "Normal", "Low", "Minimum"]));

        // Control Settings
        Options.Add(new GameOption(UiControlOption.MoveMode, gameConfig).Initialize("Movement Settings", ["Standard", "Legacy"]));
        Options.Add(new GameOption(UiConfigOption.FPSCameraInterpolationType, gameConfig).Initialize("1st Person Camera Auto Adjust", ["Only When Moving", "Always", "Never"]));
        Options.Add(new GameOption(UiConfigOption.EventCameraAutoControl, gameConfig).Initialize("Look at target when speaking"));
        Options.Add(new GameOption(UiControlOption.ObjectBorderingType, gameConfig).Initialize("Highlight Targets"));

        Options.Add(new GameOption(UiConfigOption.BattleEffectSelf, gameConfig).Initialize("Battle Effects Self", ["Show All", "Show Limited", "Show None"]));
        Options.Add(new GameOption(UiConfigOption.BattleEffectParty, gameConfig).Initialize("Battle Effects Party", ["Show All", "Show Limited", "Show None"]));
        Options.Add(new GameOption(UiConfigOption.BattleEffectOther, gameConfig).Initialize("Battle Effects Other", ["Show All", "Show Limited", "Show None"]));
        Options.Add(new GameOption(UiConfigOption.BattleEffectPvPEnemyPc, gameConfig).Initialize("Battle Effects PVP", ["Show All", "Show Limited", "Show None"]));

        Options.Add(new GameOption(UiConfigOption.NamePlateDispTypeOther, gameConfig).Initialize("Other PC Nameplates", ["Always", "During Battle", "When Targeted", "Never", "Out of Battle"]));
    }

    private Dictionary<string, uint>? OriginalSettings;
    public void Apply()
    {
        if (OriginalSettings != null)
        {
            return;
        }
        var original = new Dictionary<string, uint>();
        Options.ForEach(o =>
        {
            if (configuration.GetVRGameSetting(o.GetID()) is uint value)
            {
                logger.Debug($"Setting {o.GetID()} to {value}");
                original[o.GetID()] = o.GetCurrentValue();
                o.SetValue(value);
            }
        });
        OriginalSettings = original;
    }
    public void Revert()
    {
        if (OriginalSettings is Dictionary<string, uint> toRevert)
        {
            Options.ForEach(o =>
            {
                if (toRevert.ContainsKey(o.GetID()))
                {
                    var value = toRevert[o.GetID()];
                    logger.Debug($"Reverting {o.GetID()} to {value}");
                    o.SetValue(value);
                }
            });
            OriginalSettings = null;
        }
    }
}