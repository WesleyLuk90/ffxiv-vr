use core::ffi::c_void;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
    D3D11_MAPPED_SUBRESOURCE, D3D11_PS_CS_UAV_REGISTER_COUNT,
    D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, ID3D11DepthStencilView, ID3D11DeviceContext,
    ID3D11RenderTargetView, ID3D11Resource, ID3D11ShaderResourceView, ID3D11UnorderedAccessView,
    ID3D11View,
};
use windows::core::Interface;

use crate::AppState;
use crate::eye_resources::{Access, MIRRORED_EYE, ViewKind};
use crate::hooks;
use crate::modifiers::Pass;
use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};
use crate::modifiers::clip_to_world_matrix::{ClipToWorldMatrix, modify_clip_to_world_matrix};
use crate::modifiers::decal_parameter::{DecalParameter, modify_decal_parameter};
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

const SHADER_RESOURCE_SLOT_COUNT: usize = D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT as usize;
const UNORDERED_ACCESS_SLOT_COUNT: usize = D3D11_PS_CS_UAV_REGISTER_COUNT as usize;
const RENDER_TARGET_SLOT_COUNT: usize = D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT as usize;

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
}

pub(crate) fn om_set_render_targets(
    app: &mut AppState,
    num_views: u32,
    render_target_views: *mut *mut c_void,
    depth_stencil_view: *mut c_void,
) {
    app.current_pass = current_pass(app, num_views, render_target_views, depth_stencil_view);
}

fn current_pass(
    app: &AppState,
    num_views: u32,
    render_target_views: *mut *mut c_void,
    depth_stencil_view: *mut c_void,
) -> Option<Pass> {
    if render_target_views.is_null() {
        return None;
    }
    let views = unsafe { std::slice::from_raw_parts(render_target_views, num_views as usize) };
    views.iter().copied().find_map(|view| {
        let resource = render_target_resource(view)?;
        if resource == app.geometry_texture {
            Some(Pass::Geometry)
        } else if resource == app.composition_texture && !depth_stencil_view.is_null() {
            Some(Pass::Compositing)
        } else {
            None
        }
    })
}

fn render_target_resource(view: *mut c_void) -> Option<*mut c_void> {
    let typed = unsafe { ID3D11RenderTargetView::from_raw_borrowed(&view) }?;
    let resource = unsafe { typed.GetResource() }.ok()?;
    Some(Interface::as_raw(&resource))
}

/// # Safety
/// `data` must be valid for `byte_width` bytes, writable, and correctly aligned for whichever of
/// `CameraParameters`/`VSViewProjectionMatrix`/`PSViewProjectionInverseMatrix`/`ClipToWorldMatrix`/`WorldViewProjMatrix`/
/// `ProjectionMatrix`/`LightParamBuffer`/`DecalParameter`/`SkyQuadParam`/`SunParam`/`RadialBlurParam`/
/// `LensFlareParam` `byte_width` selects.
unsafe fn try_modify_camera_buffer(app: &mut AppState, byte_width: u32, data: *mut c_void) -> bool {
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

pub(crate) fn map(
    app: &mut AppState,
    resource: *mut c_void,
    subresource: u32,
    map_type: u32,
    mapped_resource: *mut c_void,
    unhooked: &dyn Fn() -> i32,
) -> i32 {
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
}

pub(crate) fn unmap(
    app: &mut AppState,
    context: *mut ID3D11DeviceContext,
    resource: *mut c_void,
    subresource: u32,
    unhooked: &dyn Fn(),
) {
    let Some((data, byte_width)) = app.shadow_buffers.take_pending(resource, subresource) else {
        unhooked();
        return;
    };
    if app.camera.has_active_eye() {
        unsafe { try_modify_camera_buffer(app, byte_width, data) };
    }
    flush_shadow_to_gpu(app, context, resource, subresource);
}

fn flush_shadow_to_gpu(
    app: &AppState,
    context: *mut ID3D11DeviceContext,
    resource: *mut c_void,
    subresource: u32,
) {
    let Some(data) = app.shadow_buffers.data(resource) else {
        return;
    };
    let mut mapped = D3D11_MAPPED_SUBRESOURCE::default();
    let mapped_ptr = &mut mapped as *mut D3D11_MAPPED_SUBRESOURCE as *mut c_void;
    let hr = hooks::call_original(&hooks::map::DETOUR, |hook| unsafe {
        hook.call(
            context,
            resource,
            subresource,
            D3D11_MAP_WRITE_DISCARD,
            0,
            mapped_ptr,
        )
    });
    if !matches!(hr, Some(hr) if hr >= 0) {
        return;
    }
    unsafe {
        std::ptr::copy_nonoverlapping(data.as_ptr(), mapped.pData as *mut u8, data.len());
    }
    hooks::call_original(&hooks::unmap::DETOUR, |hook| unsafe {
        hook.call(context, resource, subresource)
    });
}

pub(crate) fn compute_camera_update_subresource(
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
    let ptr = app.shadow_buffers.load_update_subresource_scratch(src);
    let modified = unsafe { try_modify_camera_buffer(app, byte_width, ptr) };
    modified.then_some(app.shadow_buffers.update_subresource_scratch())
}

fn view_slice<'a>(views: *const *mut c_void, num_views: u32) -> Option<&'a [*mut c_void]> {
    if views.is_null() || num_views == 0 {
        return None;
    }
    Some(unsafe { std::slice::from_raw_parts(views, num_views as usize) })
}

fn is_tracking(app: &AppState) -> bool {
    app.camera.has_active_eye()
}

fn is_mirrored_eye(app: &AppState) -> bool {
    app.camera.active_eye() == Some(MIRRORED_EYE)
}

fn view_resource(view: *mut c_void) -> Option<ID3D11Resource> {
    let typed = unsafe { ID3D11View::from_raw_borrowed(&view) }?;
    unsafe { typed.GetResource() }.ok()
}

fn record_access(app: &mut AppState, resource: &ID3D11Resource, access: Access) {
    let excluded = [app.geometry_texture, app.composition_texture];
    let Some(created) = app
        .eye_resources
        .record(&app.device, resource, access, &excluded)
    else {
        return;
    };
    let context = app.device_context;
    hooks::call_original(&hooks::copy_resource::DETOUR, |hook| unsafe {
        hook.call(context, created.mirror.as_raw(), created.original.as_raw())
    });
}

pub(crate) fn eye_views(
    app: &mut AppState,
    kind: ViewKind,
    access: Access,
    views: *const *mut c_void,
    num_views: u32,
) -> Option<Vec<*mut c_void>> {
    if !is_tracking(app) {
        return None;
    }
    let views = view_slice(views, num_views)?;
    for &view in views {
        if let Some(resource) = view_resource(view) {
            record_access(app, &resource, access);
        }
    }
    if !is_mirrored_eye(app) {
        return None;
    }
    app.eye_resources.substitute(&app.device, kind, views)
}

pub(crate) fn eye_view(
    app: &mut AppState,
    kind: ViewKind,
    access: Access,
    view: *mut c_void,
) -> *mut c_void {
    eye_views(app, kind, access, &view, 1).map_or(view, |views| views[0])
}

pub(crate) fn eye_resource(
    app: &mut AppState,
    access: Access,
    resource: *mut c_void,
) -> *mut c_void {
    if !is_tracking(app) {
        return resource;
    }
    let Some(typed) = (unsafe { ID3D11Resource::from_raw_borrowed(&resource) }).cloned() else {
        return resource;
    };
    record_access(app, &typed, access);
    if !is_mirrored_eye(app) {
        return resource;
    }
    app.eye_resources
        .mirror_resource(resource)
        .unwrap_or(resource)
}

type ShaderResourcesDetour =
    std::sync::Mutex<Option<retour::GenericDetour<hooks::ps_set_shader_resources::Fn>>>;
type ShaderResourcesGetter = fn(&ID3D11DeviceContext, &mut [Option<ID3D11ShaderResourceView>]);

fn shader_resource_stages() -> [(&'static ShaderResourcesDetour, ShaderResourcesGetter); 6] {
    [
        (
            &hooks::vs_set_shader_resources::DETOUR,
            |context, views| unsafe { context.VSGetShaderResources(0, Some(views)) },
        ),
        (
            &hooks::hs_set_shader_resources::DETOUR,
            |context, views| unsafe { context.HSGetShaderResources(0, Some(views)) },
        ),
        (
            &hooks::ds_set_shader_resources::DETOUR,
            |context, views| unsafe { context.DSGetShaderResources(0, Some(views)) },
        ),
        (
            &hooks::gs_set_shader_resources::DETOUR,
            |context, views| unsafe { context.GSGetShaderResources(0, Some(views)) },
        ),
        (
            &hooks::ps_set_shader_resources::DETOUR,
            |context, views| unsafe { context.PSGetShaderResources(0, Some(views)) },
        ),
        (
            &hooks::cs_set_shader_resources::DETOUR,
            |context, views| unsafe { context.CSGetShaderResources(0, Some(views)) },
        ),
    ]
}

fn swapped_view(
    app: &mut AppState,
    kind: ViewKind,
    view: *mut c_void,
    to_mirror: bool,
) -> Option<*mut c_void> {
    if view.is_null() {
        return None;
    }
    if to_mirror {
        app.eye_resources.mirror_view(&app.device, kind, view)
    } else {
        app.eye_resources.original_view(view)
    }
}

pub(crate) fn sync_bound_views(app: &mut AppState) {
    if !app.eye_resources.has_mirrors() {
        return;
    }
    let to_mirror = app.hooks_enabled && is_mirrored_eye(app);
    let context_ptr = app.device_context;
    let raw_context = context_ptr as *mut c_void;
    let Some(context) = (unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }).cloned()
    else {
        return;
    };

    for (detour, get) in shader_resource_stages() {
        let mut views: [Option<ID3D11ShaderResourceView>; SHADER_RESOURCE_SLOT_COUNT] =
            std::array::from_fn(|_| None);
        get(&context, &mut views);
        for (slot, view) in views.iter().enumerate() {
            let Some(view) = view else {
                continue;
            };
            let Some(swapped) =
                swapped_view(app, ViewKind::ShaderResource, view.as_raw(), to_mirror)
            else {
                continue;
            };
            hooks::call_original(detour, |hook| unsafe {
                hook.call(context_ptr, slot as u32, 1, &swapped)
            });
        }
    }

    let mut uavs: [Option<ID3D11UnorderedAccessView>; UNORDERED_ACCESS_SLOT_COUNT] =
        Default::default();
    unsafe { context.CSGetUnorderedAccessViews(0, Some(&mut uavs)) };
    for (slot, uav) in uavs.iter().enumerate() {
        let Some(uav) = uav else {
            continue;
        };
        let Some(swapped) = swapped_view(app, ViewKind::UnorderedAccess, uav.as_raw(), to_mirror)
        else {
            continue;
        };
        let keep_counter = u32::MAX;
        hooks::call_original(
            &hooks::cs_set_unordered_access_views::DETOUR,
            |hook| unsafe { hook.call(context_ptr, slot as u32, 1, &swapped, &keep_counter) },
        );
    }

    let mut rtvs: [Option<ID3D11RenderTargetView>; RENDER_TARGET_SLOT_COUNT] = Default::default();
    let mut dsv: Option<ID3D11DepthStencilView> = None;
    unsafe { context.OMGetRenderTargets(Some(&mut rtvs), Some(&mut dsv)) };
    let mut rtv_ptrs: Vec<*mut c_void> = rtvs
        .iter()
        .map(|view| {
            view.as_ref()
                .map_or(std::ptr::null_mut(), Interface::as_raw)
        })
        .collect();
    let mut dsv_ptr = dsv.as_ref().map_or(std::ptr::null_mut(), Interface::as_raw);
    let mut changed = false;
    for ptr in rtv_ptrs.iter_mut() {
        if let Some(swapped) = swapped_view(app, ViewKind::RenderTarget, *ptr, to_mirror) {
            *ptr = swapped;
            changed = true;
        }
    }
    if let Some(swapped) = swapped_view(app, ViewKind::DepthStencil, dsv_ptr, to_mirror) {
        dsv_ptr = swapped;
        changed = true;
    }
    if !changed {
        return;
    }
    let count = rtv_ptrs
        .iter()
        .rposition(|ptr| !ptr.is_null())
        .map_or(0, |last| last + 1);
    hooks::call_original(
        &hooks::om_set_render_targets_and_unordered_access_views::DETOUR,
        |hook| unsafe {
            hook.call(
                context_ptr,
                count as u32,
                rtv_ptrs.as_ptr(),
                dsv_ptr,
                0,
                D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
                std::ptr::null(),
                std::ptr::null(),
            )
        },
    );
}
