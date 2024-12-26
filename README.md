# FFXIV VR - A VR plugin for FFXIV

Implemented entirely in C# using the Silk OpenXR bindings.

Based on the original [xivr-Ex Plugin](https://github.com/ProjectMimer/xivr-Ex)

## Features

- VR in FFXIV
- First person mode with body view
- Customizable VR Controller bindings
- Hand tracking
- Controller hand tracking

## Instructions

1. Completely delete GShade and ReShade, check that the DLL files have been removed from your FFXIV folder, they might cause crashes with this plugin
2. Add `https://raw.githubusercontent.com/WesleyLuk90/ffxiv-vr/refs/heads/master/PluginRepo/pluginmaster.json` as a custom plugin repo in Dalamud settings
3. Install the FFXIV VR plugin
4. If you're using SteamVR enable `Dalamud Settings > Wait for plugins before game loads` and restart the game
5. VR will not work if the game is set to full screen, please use borderless window mode (preferred) or window mode
6. Run `/vr start` to start VR, run `/vr stop` to stop
7. Run `/vr` to access settings

## Tips

- For performance, decreases the display limits in FFXIV
  - `System Configuration > ... > Display Limits > Character and Object Quantity > Minimum`
- To remove the ghost outline around characters
  - `System Configuration > Graphics Settings -> Edge Smoothing (Anti-aliasing) > Off (or FXAA)`

## Help

If you want to report a bug or request a feature please create an [Issue](https://github.com/WesleyLuk90/ffxiv-vr/issues).

For support and discussion please ask in the Discord ([https://discord.gg/flat2vr](https://discord.gg/flat2vr))

## Compatibility

Tested with Virtual Desktop, Occulus Link and SteamVR. OpenXR is recomended over SteamVR if available.

## Troubleshooting

If you're using Quest Link and running into issues, try shutting down Meta Quest Link and restarting the Occulus VR Runtime Service in Windows.

Sometimes SteamVR will get stuck waiting for the game or just take a minute. Ensure that `Dalamud Settings > Wait for plugins before game loads` is enabled and restart the game. Try waiting a bit longer or stop VR, toggle the plugin and start VR again.

GShade and ReShade must be completely deleted, check that they did not leave DLL files in your FFXIV folder, they're known to cause crashes/freezes with this plugin.

If there is a large black box in front of you there is likely an incompatible plugin enabled. Try disabling other plugins to determine if that is the cause.

Reported Incompatible Plugins

- Splatoon
- vnavmesh
- Browsingway

If you're still running into issues not working, try disabling all other plugins in Dalamud.

## Known Issues

- Mouse interaction with the game world is not aligned/does not work, UI interaction should work

## Advanced

- Configuration can be changed using commands using `/vr config <name> <value>`, e.g. `/vr config UISize 1.5`
  - For a list of available configuration see the options in the [code](https://github.com/WesleyLuk90/ffxiv-vr/blob/master/FfxivVr/config/ConfigWindow.cs#L86)
