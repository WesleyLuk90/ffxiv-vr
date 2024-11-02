# FFXIV VR - A VR plugin for FFXIV

Implemented entirely in C# using the Silk OpenXR bindings.

Heavily referenced from https://github.com/ProjectMimer/xivr-Ex

Still a work in progress but the basic VR rendering works with a floating UI.

## Instructions

1. Add `https://raw.githubusercontent.com/WesleyLuk90/ffxiv-vr/refs/heads/master/PluginRepo/pluginmaster.json` as a custom plugin repo in Dalamud settings
2. Install the FFXIV VR plugin
3. Run `/vr start` to start VR, run `/vr stop` to stop
4. Run `/vr` to access settings

## Features
* VR in FFXIV
* First person mode with body view

## Tips
* For performance, decreases the display limits in FFXIV
  * `System Configuration > ... > Display Limits > Character and Object Quantity > Minimum`

## Compatibility

There is a compatibility issue right now with SteamVR and it does not work in most cases.

| Connection | Runtime | Works | Notes |
| --- | --- | --- | --- |
| QuestLink | OpenXR/Meta Quest Link | Yes |   |
| Quest Air Link | OpenXR/Meta Quest Link | Yes |   |
| Virtual Desktop | OpenXR/VDXR | Yes |   |
| Steam Link | SteamVR | Sorta | Lots of camera issues and stuttering |