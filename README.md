# FFXIV VR - A VR plugin for FFXIV

Implemented entirely in C# using the Silk OpenXR bindings.

Based on the original [xivr-Ex Plugin](https://github.com/ProjectMimer/xivr-Ex)

## Features

-   VR in FFXIV
-   First person mode with body view
-   Hand tracking

## Instructions

1. Disable GShade and ReShade, they might cause crashes with this plugin
2. Add `https://raw.githubusercontent.com/WesleyLuk90/ffxiv-vr/refs/heads/master/PluginRepo/pluginmaster.json` as a custom plugin repo in Dalamud settings
3. Install the FFXIV VR plugin
4. If you're using SteamVR enable `Dalamud Settings > Wait for plugins before game loads` and restart the game
5. VR will not work if the game is set to full screen, please use borderless window mode (preferred) or window mode
6. Run `/vr start` to start VR, run `/vr stop` to stop
7. Run `/vr` to access settings

## Tips

-   For performance, decreases the display limits in FFXIV
    -   `System Configuration > ... > Display Limits > Character and Object Quantity > Minimum`

## Compatibility

Tested with Virtual Desktop, Occulus Link and SteamVR. OpenXR is recomended over SteamVR if available.

## Troubleshooting

If you're using Quest Link and running into issues, try shutting down Meta Quest Link and restarting the Occulus VR Runtime Service in Windows.

Sometimes SteamVR will get stuck waiting for the game or just take a minute. Try waiting a bit longer or stop VR, toggle the plugin and start VR again.

If you're running into crashes try disabling GShade and ReShade as they have been reported to cause crashes.

## Known Issues

-   No support for VR controllers
-   Mouse interaction with the game world is not aligned/does not work, UI interaction should work
