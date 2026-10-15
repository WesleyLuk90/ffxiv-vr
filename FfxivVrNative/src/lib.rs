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
    corrected_footprint_buffer: Option<*mut c_void>,
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
            corrected_footprint_buffer: None,
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

    let installed = unsafe { hooks::install_all(context) };
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
    let uninstalled = hooks::uninstall_all();
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
    app.corrected_footprint_buffer = None;
    hooks::sync_bound_views(app);
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
    app.camera.end_frame();
    true
}
