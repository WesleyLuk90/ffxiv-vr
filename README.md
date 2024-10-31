# FFXIV VR - A VR plugin for FFXIV

Implemented entirely in C# using the Silk OpenXR bindings.

Heavily referenced from https://github.com/ProjectMimer/xivr-Ex

Still a work in progress but the basic VR rendering works with a floating UI.

## Instructions

1. Download and extract the latest release
2. Add FfxivVR.dll as a dev plugin in Dalamud Settings > Experimental > Dev Plugin Locations
3. Run `/vr start` to start VR, run `/vr stop` to stop

## Features
* VR in FFXIV
* First person mode with body view

## Tips
* For performance, decreases the display limits in FFXIV
  * `System Configuration > ... > Display Limits > Character and Object Quantity > Minimum`