use core::ffi::c_void;
use std::ffi::CStr;
use std::fs::OpenOptions;
use std::sync::{Mutex, OnceLock};

use log::LevelFilter;
use simplelog::WriteLogger;
use windows::Win32::Graphics::Direct3D11::{ID3D11Device, ID3D11DeviceContext};
use windows::core::Interface;

mod camera_state;
mod eye_resources;
mod hook_bodies;
mod hooks;
mod math;
mod modifiers;
mod shadow_buffers;

use camera_state::CameraState;
use eye_resources::EyeResources;
use math::Mat4;
use modifiers::Pass;
use modifiers::sun_modifier::SunModifier;
use shadow_buffers::ShadowBuffers;

struct AppState {
    device_context: *mut ID3D11DeviceContext,
    device: ID3D11Device,
    hooks_enabled: bool,

    camera: CameraState,
    sun_modifier: SunModifier,
    shadow_buffers: ShadowBuffers,
    eye_resources: EyeResources,

    geometry_texture: *mut c_void,
    composition_texture: *mut c_void,
    current_pass: Option<Pass>,
}

unsafe impl Send for AppState {}

impl AppState {
    /// # Safety
    /// `context` must be a valid, non-null `ID3D11DeviceContext*` for the game's immediate
    /// context.
    unsafe fn new(
        context: *mut ID3D11DeviceContext,
        geometry_texture: *mut c_void,
        composition_texture: *mut c_void,
    ) -> Option<Self> {
        let raw = context as *mut c_void;
        let ctx = unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw) }?;
        let device = unsafe { ctx.GetDevice() }.ok()?;

        Some(Self {
            device_context: context,
            device: device.clone(),
            hooks_enabled: false,
            camera: CameraState::new(),
            sun_modifier: SunModifier::default(),
            shadow_buffers: ShadowBuffers::default(),
            eye_resources: EyeResources::default(),
            geometry_texture,
            composition_texture,
            current_pass: None,
        })
    }
}

static APP: Mutex<Option<AppState>> = Mutex::new(None);

static LOGGER_INIT: OnceLock<()> = OnceLock::new();

fn c_str_to_string(ptr: *const u8) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    Some(
        unsafe { CStr::from_ptr(ptr.cast()) }
            .to_string_lossy()
            .into_owned(),
    )
}

fn init_logger(log_dir: *const u8) {
    let Some(log_dir) = c_str_to_string(log_dir) else {
        return;
    };
    LOGGER_INIT.get_or_init(|| {
        if std::fs::create_dir_all(&log_dir).is_err() {
            return;
        }
        let log_path = std::path::Path::new(&log_dir).join("ffxiv_vr_native.log");
        if let Ok(file) = OpenOptions::new().create(true).append(true).open(&log_path) {
            let _ = WriteLogger::init(LevelFilter::Debug, simplelog::Config::default(), file);
        }
    });
}

macro_rules! for_each_hook {
    ($action:ident) => {
        $action!(
            (om_set_render_targets, om_set_render_targets_detour),
            (
                om_set_render_targets_and_unordered_access_views,
                om_set_render_targets_and_unordered_access_views_detour
            ),
            (map, map_detour),
            (unmap, unmap_detour),
            (update_subresource, update_subresource_detour),
            (dispatch, dispatch_detour),
            (dispatch_indirect, dispatch_indirect_detour),
            (vs_set_shader_resources, vs_set_shader_resources_detour),
            (hs_set_shader_resources, hs_set_shader_resources_detour),
            (ds_set_shader_resources, ds_set_shader_resources_detour),
            (gs_set_shader_resources, gs_set_shader_resources_detour),
            (ps_set_shader_resources, ps_set_shader_resources_detour),
            (cs_set_shader_resources, cs_set_shader_resources_detour),
            (
                cs_set_unordered_access_views,
                cs_set_unordered_access_views_detour
            ),
            (copy_subresource_region, copy_subresource_region_detour),
            (copy_resource, copy_resource_detour),
            (resolve_subresource, resolve_subresource_detour),
            (clear_render_target_view, clear_render_target_view_detour),
            (
                clear_unordered_access_view_uint,
                clear_unordered_access_view_uint_detour
            ),
            (
                clear_unordered_access_view_float,
                clear_unordered_access_view_float_detour
            ),
            (clear_depth_stencil_view, clear_depth_stencil_view_detour),
            (generate_mips, generate_mips_detour),
        )
    };
}

/// # Safety
/// `context` must be a valid, non-null `ID3D11DeviceContext*` for the game's immediate context.
unsafe fn install_hooks(context: *mut ID3D11DeviceContext) -> bool {
    macro_rules! install {
        ($(($hook:ident, $detour:ident)),* $(,)?) => {
            true $(&& unsafe {
                hooks::install_hook(
                    context,
                    hooks::$hook::INDEX,
                    hooks::$detour as hooks::$hook::Fn,
                    &hooks::$hook::DETOUR,
                )
            })*
        };
    }
    for_each_hook!(install)
}

fn uninstall_hooks() -> bool {
    macro_rules! uninstall {
        ($(($hook:ident, $detour:ident)),* $(,)?) => {
            true $(& hooks::uninstall_hook(&hooks::$hook::DETOUR))*
        };
    }
    for_each_hook!(uninstall)
}

/// # Safety
/// `context` must be a valid, non-null `ID3D11DeviceContext*` for the game's immediate context.
#[unsafe(no_mangle)]
pub unsafe extern "system" fn ffxiv_vr_native_init(
    context: *mut ID3D11DeviceContext,
    _width: u32,
    _height: u32,
    composition_texture: *mut c_void,
    geometry_texture: *mut c_void,
    log_dir: *const u8,
) -> bool {
    init_logger(log_dir);
    if context.is_null() {
        return false;
    }
    let Some(app_state) =
        (unsafe { AppState::new(context, geometry_texture, composition_texture) })
    else {
        return false;
    };

    let installed = unsafe { install_hooks(context) };
    if !installed {
        return false;
    }
    let Ok(mut guard) = APP.lock() else {
        return false;
    };
    *guard = Some(app_state);
    log::info!("ffxiv_vr_native initialized");
    true
}

#[unsafe(no_mangle)]
pub extern "system" fn ffxiv_vr_native_shutdown() -> bool {
    let uninstalled = uninstall_hooks();
    if let Ok(mut guard) = APP.lock() {
        *guard = None;
    }
    uninstalled
}

#[unsafe(no_mangle)]
pub extern "system" fn ffxiv_vr_native_set_config(hooks_enabled: bool) -> bool {
    let Ok(mut guard) = APP.lock() else {
        return false;
    };
    let Some(app) = guard.as_mut() else {
        return false;
    };
    app.hooks_enabled = hooks_enabled;
    true
}

/// # Safety
/// `view` and `projection`, if non-null, must each point to 16 valid, readable `f32`s.
#[unsafe(no_mangle)]
pub unsafe extern "system" fn ffxiv_vr_native_set_projection_matrix(
    eye: u32,
    view: *const f32,
    projection: *const f32,
) -> bool {
    if view.is_null() {
        return false;
    }
    let Some(view) = (unsafe { read_mat4(view) }) else {
        return false;
    };
    let projection = if projection.is_null() {
        None
    } else {
        match unsafe { read_mat4(projection) } {
            Some(projection) => Some(projection),
            None => return false,
        }
    };
    let Ok(mut guard) = APP.lock() else {
        return false;
    };
    let Some(app) = guard.as_mut() else {
        return false;
    };
    app.camera.update_camera(eye as usize, view, projection)
}

#[unsafe(no_mangle)]
pub extern "system" fn ffxiv_vr_native_set_active_eye(eye: i32) -> bool {
    let Ok(mut guard) = APP.lock() else {
        return false;
    };
    let Some(app) = guard.as_mut() else {
        return false;
    };
    let accepted = app.camera.set_active_eye(usize::try_from(eye).ok());
    app.sun_modifier.reset();
    app.eye_resources.begin_pass();
    hook_bodies::sync_bound_views(app);
    accepted
}

/// # Safety
/// `matrix` must be non-null and point to 16 valid, readable `f32`s.
unsafe fn read_mat4(matrix: *const f32) -> Option<Mat4> {
    let slice = unsafe { std::slice::from_raw_parts(matrix, 16) };
    let array: [f32; 16] = slice.try_into().ok()?;
    Some(Mat4::from_row_major(&array))
}

#[unsafe(no_mangle)]
pub extern "system" fn ffxiv_vr_native_on_frame_end() -> bool {
    let Ok(mut guard) = APP.lock() else {
        return false;
    };
    let Some(app) = guard.as_mut() else {
        return false;
    };
    if let Err(err) = unsafe { app.device.GetDeviceRemovedReason() } {
        log::error!(
            "D3D11 device removed: {err} (0x{:08X})",
            err.code().0 as u32
        );
    }
    app.eye_resources.end_frame();
    true
}
