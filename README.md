# FFXIV VR - A VR plugin for FFXIV

Implemented entirely in C# using the Silk OpenXR bindings.

Heavily referenced from https://github.com/ProjectMimer/xivr-Ex

Still a work in progress but the basic VR rendering works with a floating UI.

## Instructions

1. Add `https://raw.githubusercontent.com/WesleyLuk90/ffxiv-vr/refs/heads/master/PluginRepo/pluginmaster.json` as a custom plugin repo in Dalamud settings
2. Install the FFXIV VR plugin
3. If you're using SteamVR enable `Dalamud Settings > Wait for plugins before game loads` and restart the game
4. Run `/vr start` to start VR, run `/vr stop` to stop
5. Run `/vr` to access settings

## Features
* VR in FFXIV
* First person mode with body view

## Tips
* For performance, decreases the display limits in FFXIV
  * `System Configuration > ... > Display Limits > Character and Object Quantity > Minimum`

## Compatibility

The follow connection methods have been tried:

| Connection | Runtime | Works |
| --- | --- | --- |
| QuestLink | OpenXR/Meta Quest Link | Yes |
| Quest Air Link | OpenXR/Meta Quest Link | Yes |
| Virtual Desktop | OpenXR/VDXR | Yes |
| Wired | SteamVR | Yes |
| Steam Link | SteamVR | Yes |

## Troubleshooting
If you're using Quest Link and running into issues, try shutting down Meta Quest Link and restarting the Occulus VR Runtime Service in Windows.

Sometimes SteamVR will get stuck waiting for the game or just take a minute. Try waiting a bit longer or stop VR, toggle the plugin and start VR again.