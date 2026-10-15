use super::*;
use windows::Win32::Graphics::Direct3D11::D3D11_MAPPED_SUBRESOURCE;

use crate::modifiers::Pass;
use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};
use crate::modifiers::clip_to_world_matrix::{ClipToWorldMatrix, modify_clip_to_world_matrix};
use crate::modifiers::decal_parameter::{DecalParameter, modify_decal_parameter};
use crate::modifiers::footprint_instances::{FootprintInstances, modify_footprint_instances};
use crate::modifiers::light_param::{LightParamBuffer, modify_light_param};
use crate::modifiers::projection_matrix::{ProjectionMatrix, modify_compositing_projection_matrix};
use crate::modifiers::ps_view_projection_inverse_matrix::{
    PSViewProjectionInverseMatrix, modify_geometry_ps_view_projection_inverse_matrix,
};
use crate::modifiers::sky_quad_param::{SkyQuadParam, modify_sky_quad_param};
use crate::modifiers::sun_modifier::{LensFlareParam, RadialBlurParam, SunParam};
use crate::modifiers::vs_view_projection_matrix::{
    VSViewProjectionMatrix, modify_geometry_vs_view_projection_matrix,
};
use crate::modifiers::world_view_proj_matrix::{
    WorldViewProjMatrix, modify_world_view_proj_matrix,
};

const D3D11_MAP_WRITE: u32 = 2;
const D3D11_MAP_WRITE_DISCARD: u32 = 4;
const D3D11_MAP_WRITE_NO_OVERWRITE: u32 = 5;

fn is_write_map(map_type: u32) -> bool {
    matches!(
        map_type,
        D3D11_MAP_WRITE | D3D11_MAP_WRITE_DISCARD | D3D11_MAP_WRITE_NO_OVERWRITE
    )
}

const CAMERA_PARAMETERS_SIZE: u32 = std::mem::size_of::<CameraParameters>() as u32;
const VS_VIEW_PROJECTION_MATRIX_SIZE: u32 = std::mem::size_of::<VSViewProjectionMatrix>() as u32;
const PS_VIEW_PROJECTION_INVERSE_MATRIX_SIZE: u32 =
    std::mem::size_of::<PSViewProjectionInverseMatrix>() as u32;
const WORLD_VIEW_PROJ_MATRIX_SIZE: u32 = std::mem::size_of::<WorldViewProjMatrix>() as u32;
const LIGHT_PARAM_BUFFER_SIZE: u32 = std::mem::size_of::<LightParamBuffer>() as u32;
const SKY_QUAD_PARAM_SIZE: u32 = std::mem::size_of::<SkyQuadParam>() as u32;
const SUN_PARAM_SIZE: u32 = std::mem::size_of::<SunParam>() as u32;
const LENS_FLARE_PARAM_SIZE: u32 = std::mem::size_of::<LensFlareParam>() as u32;
const RADIAL_BLUR_PARAM_SIZE: u32 = std::mem::size_of::<RadialBlurParam>() as u32;
pub(super) const FOOTPRINT_INSTANCES_SIZE: u32 = std::mem::size_of::<FootprintInstances>() as u32;

fn is_camera_buffer_size(app: &AppState, byte_width: u32) -> bool {
    if byte_width == RADIAL_BLUR_PARAM_SIZE {
        return app.sun_modifier.is_tracking_sun();
    }
    byte_width == CAMERA_PARAMETERS_SIZE
        || byte_width == VS_VIEW_PROJECTION_MATRIX_SIZE
        || byte_width == PS_VIEW_PROJECTION_INVERSE_MATRIX_SIZE
        || byte_width == WORLD_VIEW_PROJ_MATRIX_SIZE
        || byte_width == LIGHT_PARAM_BUFFER_SIZE
        || byte_width == SKY_QUAD_PARAM_SIZE
        || byte_width == SUN_PARAM_SIZE
        || byte_width == FOOTPRINT_INSTANCES_SIZE
}

/// # Safety
/// `data` must be valid for `byte_width` bytes, writable, and correctly aligned for whichever of
/// `CameraParameters`/`VSViewProjectionMatrix`/`PSViewProjectionInverseMatrix`/`ClipToWorldMatrix`/`WorldViewProjMatrix`/
/// `ProjectionMatrix`/`LightParamBuffer`/`DecalParameter`/`SkyQuadParam`/`SunParam`/`RadialBlurParam`/
/// `LensFlareParam`/`FootprintInstances` `byte_width` selects.
pub(super) unsafe fn try_modify_camera_buffer(
    app: &mut AppState,
    byte_width: u32,
    data: *mut c_void,
) -> bool {
    if byte_width == FOOTPRINT_INSTANCES_SIZE {
        let buffer = unsafe { &mut *(data as *mut FootprintInstances) };
        return modify_footprint_instances(buffer, &app.camera);
    }

    if byte_width == CAMERA_PARAMETERS_SIZE {
        let buffer = unsafe { &mut *(data as *mut CameraParameters) };
        return modify_camera_parameters(buffer, &mut app.camera);
    }

    if byte_width == LIGHT_PARAM_BUFFER_SIZE {
        let decal = unsafe { &mut *(data as *mut DecalParameter) };
        if decal.is_decal_parameter() {
            return modify_decal_parameter(decal, &app.camera);
        }
        let buffer = unsafe { &mut *(data as *mut LightParamBuffer) };
        return modify_light_param(buffer, &app.camera);
    }

    if byte_width == SUN_PARAM_SIZE {
        let buffer = unsafe { &mut *(data as *mut SunParam) };
        return app.sun_modifier.modify_sun_param(buffer, &app.camera);
    }

    if byte_width == RADIAL_BLUR_PARAM_SIZE {
        let buffer = unsafe { &mut *(data as *mut RadialBlurParam) };
        return app
            .sun_modifier
            .modify_radial_blur_param(buffer, &app.camera);
    }

    if byte_width == LENS_FLARE_PARAM_SIZE {
        let buffer = unsafe { &mut *(data as *mut LensFlareParam) };
        if app
            .sun_modifier
            .modify_lens_flare_param(buffer, &app.camera)
        {
            return true;
        }
    }

    if byte_width == SKY_QUAD_PARAM_SIZE {
        let buffer = unsafe { &mut *(data as *mut SkyQuadParam) };
        if modify_sky_quad_param(buffer, &app.camera) {
            return true;
        }
    }

    let world_view_proj = unsafe { &mut *(data as *mut WorldViewProjMatrix) };
    if modify_world_view_proj_matrix(world_view_proj, &app.camera) {
        return true;
    }

    if app.current_pass == Some(Pass::Geometry) {
        let view_projection = unsafe { &mut *(data as *mut VSViewProjectionMatrix) };
        if modify_geometry_vs_view_projection_matrix(view_projection, &app.camera) {
            return true;
        }
        let view_projection_inverse = unsafe { &mut *(data as *mut PSViewProjectionInverseMatrix) };
        if modify_geometry_ps_view_projection_inverse_matrix(view_projection_inverse, &app.camera) {
            return true;
        }
        let clip_to_world = unsafe { &mut *(data as *mut ClipToWorldMatrix) };
        if modify_clip_to_world_matrix(clip_to_world, &app.camera) {
            return true;
        }
    }
    if app.current_pass == Some(Pass::Compositing) {
        let projection = unsafe { &mut *(data as *mut ProjectionMatrix) };
        if modify_compositing_projection_matrix(projection, &app.camera) {
            return true;
        }
    }
    false
}

pub(super) fn upload(
    context: *mut ID3D11DeviceContext,
    resource: *mut c_void,
    subresource: u32,
    data: &[u8],
) {
    let mut mapped = D3D11_MAPPED_SUBRESOURCE::default();
    let mapped_ptr = &mut mapped as *mut D3D11_MAPPED_SUBRESOURCE as *mut c_void;
    let hr = map::original(
        context,
        resource,
        subresource,
        D3D11_MAP_WRITE_DISCARD,
        0,
        mapped_ptr,
    );
    if !matches!(hr, Some(hr) if hr >= 0) {
        return;
    }
    unsafe {
        std::ptr::copy_nonoverlapping(data.as_ptr(), mapped.pData as *mut u8, data.len());
    }
    unmap::original(context, resource, subresource);
}

fn compute_camera_update_subresource(
    app: &mut AppState,
    dst_resource: *mut c_void,
    dst_box: *const c_void,
    src_data: *const c_void,
) -> Option<&[u8]> {
    if !app.camera.has_active_eye() || !dst_box.is_null() || src_data.is_null() {
        return None;
    }
    let byte_width = app.shadow_buffers.byte_width(dst_resource)?;
    if !is_camera_buffer_size(app, byte_width) {
        return None;
    }
    let src = unsafe { std::slice::from_raw_parts(src_data as *const u8, byte_width as usize) };
    let ptr = app.shadow_buffers.load_scratch(src);
    let modified = unsafe { try_modify_camera_buffer(app, byte_width, ptr) };
    modified.then_some(app.shadow_buffers.scratch())
}

declare_hook!(
    map,
    14,
    fn(
        context,
        resource: *mut c_void,
        subresource: u32,
        map_type: u32,
        map_flags: u32,
        mapped_resource: *mut c_void,
    ) -> i32 {
        let unhooked = || {
            original(
                context,
                resource,
                subresource,
                map_type,
                map_flags,
                mapped_resource,
            )
            .unwrap_or(-1)
        };
        run_hook(
            context,
            |app, unhooked| {
                if mapped_resource.is_null() || !is_write_map(map_type) {
                    return unhooked();
                }
                let Some(byte_width) = app.shadow_buffers.byte_width(resource) else {
                    return unhooked();
                };
                if !is_camera_buffer_size(app, byte_width) {
                    return unhooked();
                }
                let data = app
                    .shadow_buffers
                    .begin_map(resource, subresource, byte_width);
                let out = mapped_resource as *mut D3D11_MAPPED_SUBRESOURCE;
                unsafe {
                    (*out).pData = data;
                    (*out).RowPitch = byte_width;
                    (*out).DepthPitch = byte_width;
                }
                0
            },
            unhooked,
        )
    }
);

declare_hook!(
    unmap,
    15,
    fn(context, resource: *mut c_void, subresource: u32) {
        run_hook(
            context,
            |app, unhooked| {
                let Some((data, byte_width)) =
                    app.shadow_buffers.take_pending(resource, subresource)
                else {
                    unhooked();
                    return;
                };
                if app.camera.has_active_eye() {
                    let shadow =
                        unsafe { std::slice::from_raw_parts(data as *const u8, byte_width as usize) };
                    let scratch = app.shadow_buffers.load_scratch(shadow);
                    if unsafe { try_modify_camera_buffer(app, byte_width, scratch) } {
                        upload(context, resource, subresource, app.shadow_buffers.scratch());
                        return;
                    }
                }
                if let Some(shadow) = app.shadow_buffers.data(resource) {
                    upload(context, resource, subresource, shadow);
                }
            },
            || {
                original(context, resource, subresource);
            },
        );
    }
);

declare_hook!(
    update_subresource,
    48,
    fn(
        context,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        dst_box: *const c_void,
        src_data: *const c_void,
        src_row_pitch: u32,
        src_depth_pitch: u32,
    ) {
        let call = |dst_resource, src_data| {
            original(
                context,
                dst_resource,
                dst_subresource,
                dst_box,
                src_data,
                src_row_pitch,
                src_depth_pitch,
            );
        };
        run_hook(
            context,
            |app, _unhooked| {
                let modified =
                    compute_camera_update_subresource(app, dst_resource, dst_box, src_data);
                let effective_src =
                    modified.map_or(src_data, |bytes| bytes.as_ptr() as *const c_void);
                let effective_dst = eye_resource(app, Access::Write, dst_resource);
                call(effective_dst, effective_src);
            },
            || call(dst_resource, src_data),
        );
    }
);
