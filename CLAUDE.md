## Conventions

No comments. Don't write them in any file you touch. The one exception: `# Safety` doc comments stay even on functions that aren't `unsafe fn` - see the native DLL section below.

## Structure

The `FFXIVClientStructs` and `Dalamud` folders contain the sources for the dependencies (vendored checkouts, not this project's own code).

## What this is

A Dalamud plugin (`FfxivVr`, C#/.NET) that adds VR (OpenXR) support to FFXIV, plus a native Rust DLL (`FfxivVrNative`) that hooks the game's D3D11 device-context calls to rewrite the camera/decal/UI/light constant buffers the game writes with per-eye view/projection data, and to give the right eye its own copy of every frame-to-frame history resource. `FfxivVr.csproj` builds the Rust DLL and compiles the HLSL shaders as part of its own build, so a normal `dotnet build` produces everything needed.

There are two implementations of stereo rendering, selected via the `IRenderStrategy` interface: `SinglePassRenderStrategy` (the game's camera sits at the center of the two eyes; it drives two full render passes - left eye, then right - arming `FfxivVrNative`'s constant-buffer rewriting around each so both are reprojected from the center camera to that eye) and `AltEyeRenderStrategy` (doesn't depend on the native DLL).

## Build

- C# (`ffxiv-vr.sln`): `dotnet build --configuration Release`
- Rust (`FfxivVrNative/`): `cargo build --release`

`dotnet build` on `FfxivVr` also runs `cargo build --release` for `FfxivVrNative` and recompiles the HLSL shaders, so a plain C# build picks up changes to either.

## Architecture

### C# plugin (`FfxivVr/`)

- `Plugin.cs` is the Dalamud entry point. It builds a DI container via `AppFactory.CreateSession()` (`Microsoft.Extensions.Hosting`) and initializes the render pipeline injector, game hooks, command handling, game events, and the debug/config UI from it.
- VR on/off is driven by `VRStartStop` → `VRLifecycle` → `VRSession`, which owns the active `IRenderStrategy` (see above).
- `RenderPipelineInjector` locates and patches into the game's own render command queue via signature scanning (`ISigScanner`), independent of the D3D11-hook-based constant-buffer rewriting in `FfxivVrNative`.
- `game/` holds game-state/hook glue (`GameHooks`, `GameTextures`, `GameEvents`, extended FFXIVClientStructs structs); `vr/` holds the OpenXR session/swapchain/camera/input code; `math/` holds skeleton/IK/camera math; `config/`/`debug/` are the settings and debug (ImGui) windows.
- `FfxivVr/native/FfxivVrNative.cs` is the C# side of the FFI boundary to the Rust DLL: it loads `ffxiv_vr_native.dll` via `NativeLibrary` and exposes each `ffxiv_vr_native_*` export as a typed delegate.

### Native Rust DLL (`FfxivVrNative/`)

Builds as a `cdylib` (`ffxiv_vr_native.dll`). It vtable-hooks the game's `ID3D11DeviceContext` methods (via `retour`) - `OMSetRenderTargets`, `Map`, `Unmap`, `UpdateSubresource` for the camera buffers, plus every view-binding/clear/copy call for per-eye history resources - to track which render pass (geometry/compositing) is currently bound and rewrite the camera/decal/UI/light constant buffers the game writes during that pass with the active eye's view/projection data, so each of the two render passes draws from its own eye's VR camera instead of the center camera the game set up. All mutable state lives in one `AppState` behind a global `Mutex` (`lib.rs`), driven entirely through the hook trampolines in `hooks.rs` and the `ffxiv_vr_native_*` `extern "system"` FFI exports in `lib.rs`.

`unsafe` should only appear where code genuinely interacts with third-party/FFI surface: `windows`-crate COM method calls, `retour::GenericDetour` install/call, and raw pointers crossing the FFI boundary (the `ffxiv_vr_native_*` exports, the hook trampolines in `hooks.rs`, `AppState::new`, `hooks::vtable_entry`, `hook_bodies::try_modify_camera_buffer`). A function should be a plain `fn`, not `unsafe fn`, just because it calls something else that's internally unsafe — mark it `unsafe fn` only if it itself takes a raw, untyped pointer with a precondition the type system can't express. Keep the `unsafe { ... }` block as small as possible (wrapping just the actual COM/FFI call), and keep any `# Safety` doc comment even after dropping `unsafe fn` from a function's signature — it still documents a real invariant, just one already guaranteed by construction (e.g. a raw pointer argument only ever being live for the duration of the hook call that produced it) rather than one each caller needs to separately prove.

This crate started as a Rust port of a former C# `FfxivVr/vr/SinglePassRenderStrategy/` folder; that C# folder no longer exists (fully replaced), and the code no longer documents itself by reference to it - see Conventions above.

Key modules:

- `lib.rs` — owns the global `AppState` behind a `Mutex`, defines every `ffxiv_vr_native_*` FFI export (`init`/`shutdown`/`set_config`/`set_projection_matrix`/`set_manual_second_pass_active`/`on_frame_end`/...), and sets up the file logger.
- `hooks.rs` — declares the hooked vtable slots (`declare_hook!`), installs/uninstalls them via `retour::GenericDetour`, and holds the `extern "system"` detour trampolines that dispatch into `hook_bodies`.
- `hook_bodies.rs` — the actual hook logic: tracks which render pass (`Pass::Geometry`/`Pass::Compositing`) is bound from `OMSetRenderTargets`, shadows write-mapped constant buffers around `Map`/`Unmap` (and the equivalent for `UpdateSubresource`) so they can be inspected and rewritten before reaching the GPU, and dispatches to `modifiers` to rewrite the active eye's camera/decal/UI/light buffers.
- `eye_resources.rs` — per-eye copies of history resources. The game replays the same command list for both eyes, so ping-pong/history textures (AO, glare, prev depth, volume shadow) would otherwise carry one eye's output into the other. Every bind/clear/copy during an eye pass is recorded as a read or write; a resource that is written in an eye pass and also read before being written in some pass gets a same-desc mirror (2D textures only, excluding the geometry/composition textures and tiny textures). While the right eye is active, every render-target/depth/UAV/SRV bind, clear, and copy is swapped to the mirror, and `hook_bodies::sync_bound_views` re-swaps whatever is already bound at each eye switch.
- `camera_state.rs` — `CameraState`, the per-eye view/projection overrides set over FFI, the active eye, and the game's original center projection (captured by `modify_camera_parameters`); every `modify_...` function reads it to compute the active eye's projection.
- `shadow_buffers.rs` — `ShadowBuffers`, the CPU-side copies that write-mapped camera buffers are redirected to between `Map` and `Unmap`, plus a cache of each buffer's byte width.
- `math.rs` — `Vec4`/`Mat4` with `mul`/`invert`/`transpose`, used for all camera-matrix math.
- `modifiers/` — per-constant-buffer camera modifiers, one file per buffer type (named after the type, holding it and its `modify_...` function): `camera_parameters` (the game's main view/projection buffer), `vs_view_projection_matrix`/`ps_view_projection_inverse_matrix` (the decal shaders' `g_VS_ViewProjectionMatrix`/`g_PS_ViewProjectionInverseMatrix`), `clip_to_world_matrix` (the fog shader's row-major `cC2W` inverse view-projection), `projection_matrix` (screen-space UI element projection), `light_param` (per-light world-view-projection), `world_view_proj_matrix` (a per-draw row-major world-view-projection such as the weather-particle shaders' `g_WorldViewProjMatrix`), `sky_quad_param` (the moon's screen-space quad), `sun_modifier` (`SunModifier`, which reprojects the sun's screen-space `cSunParam` and remembers the game's original sun position for the active eye so it can recognize and reproject the god-ray `cRadialBlurParam` and lens-flare `cLensFlareParam` positioned from it); `mod.rs` holds the shared `Pass` enum.

### Shaders (`FfxivVr/shaders/`)

`VertexShader.hlsl`/`PixelShader.hlsl` are the VR compositor's own shaders (drawing the final quad to the HMD). Both compile to `.cso` and get embedded as resources in `FfxivVr.csproj`.
