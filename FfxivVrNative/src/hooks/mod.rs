use core::ffi::c_void;
use retour::{Function, GenericDetour};
use std::sync::Mutex;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
    D3D11_PS_CS_UAV_REGISTER_COUNT, D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, ID3D11DepthStencilView,
    ID3D11DeviceContext, ID3D11RenderTargetView, ID3D11Resource, ID3D11ShaderResourceView,
    ID3D11UnorderedAccessView, ID3D11View,
};
use windows::core::Interface;

use crate::eye_resources::{Access, MIRRORED_EYE, ViewKind};
use crate::{APP, AppState};

macro_rules! declare_hook {
    (@ret) => { () };
    (@ret $ret:ty) => { $ret };
    (
        $module:ident,
        $index:expr,
        fn($context:ident $(, $arg:ident : $arg_ty:ty)* $(,)?) $(-> $ret:ty)?
        $body:block
    ) => {
        pub(crate) mod $module {
            #[allow(unused_imports)]
            use super::*;

            pub type Fn = unsafe extern "system" fn(*mut ID3D11DeviceContext $(, $arg_ty)*) $(-> $ret)?;
            pub static DETOUR: Mutex<Option<GenericDetour<Fn>>> = Mutex::new(None);
            pub const INDEX: usize = $index;

            #[allow(clippy::too_many_arguments)]
            pub(crate) fn original(
                $context: *mut ID3D11DeviceContext
                $(, $arg: $arg_ty)*
            ) -> Option<declare_hook!(@ret $($ret)?)> {
                call_original(&DETOUR, |hook| unsafe { hook.call($context $(, $arg)*) })
            }

            pub(crate) unsafe extern "system" fn detour(
                $context: *mut ID3D11DeviceContext
                $(, $arg: $arg_ty)*
            ) $(-> $ret)? $body
        }
    };
}

mod buffer_updates;
mod clears;
mod copies;
mod draws;
mod render_targets;
mod shader_resources;

pub(crate) use buffer_updates::{map, unmap, update_subresource};
pub(crate) use clears::{
    clear_depth_stencil_view, clear_render_target_view, clear_unordered_access_view_float,
    clear_unordered_access_view_uint,
};
pub(crate) use copies::{copy_resource, copy_subresource_region, resolve_subresource};
pub(crate) use draws::draw_indexed_instanced;
pub(crate) use render_targets::{
    cs_set_unordered_access_views, om_set_render_targets,
    om_set_render_targets_and_unordered_access_views,
};
pub(crate) use shader_resources::{
    cs_set_shader_resources, ds_set_shader_resources, generate_mips, gs_set_shader_resources,
    hs_set_shader_resources, ps_set_shader_resources, vs_set_shader_resources,
};

macro_rules! for_each_hook {
    ($action:ident) => {
        $action!(
            om_set_render_targets,
            om_set_render_targets_and_unordered_access_views,
            map,
            unmap,
            update_subresource,
            draw_indexed_instanced,
            vs_set_shader_resources,
            hs_set_shader_resources,
            ds_set_shader_resources,
            gs_set_shader_resources,
            ps_set_shader_resources,
            cs_set_shader_resources,
            cs_set_unordered_access_views,
            copy_subresource_region,
            copy_resource,
            resolve_subresource,
            clear_render_target_view,
            clear_unordered_access_view_uint,
            clear_unordered_access_view_float,
            clear_depth_stencil_view,
            generate_mips,
        )
    };
}

/// # Safety
/// `context` must be a valid, non-null `ID3D11DeviceContext*` for the game's immediate context.
pub(crate) unsafe fn install_all(context: *mut ID3D11DeviceContext) -> bool {
    macro_rules! install {
        ($($hook:ident),* $(,)?) => {
            true $(&& unsafe {
                install_hook(context, $hook::INDEX, $hook::detour as $hook::Fn, &$hook::DETOUR)
            })*
        };
    }
    for_each_hook!(install)
}

pub(crate) fn uninstall_all() -> bool {
    macro_rules! uninstall {
        ($($hook:ident),* $(,)?) => {
            true $(& uninstall_hook(&$hook::DETOUR))*
        };
    }
    for_each_hook!(uninstall)
}

pub(crate) unsafe fn vtable_entry(
    context: *mut ID3D11DeviceContext,
    index: usize,
) -> *const c_void {
    unsafe {
        let vtable = *(context as *const *const *const c_void);
        *vtable.add(index)
    }
}

unsafe fn install_hook<F: Function>(
    context: *mut ID3D11DeviceContext,
    index: usize,
    detour_fn: F,
    slot: &Mutex<Option<GenericDetour<F>>>,
) -> bool {
    uninstall_hook(slot);
    let target_ptr = unsafe { vtable_entry(context, index) };
    if target_ptr.is_null() {
        return false;
    }
    let target = unsafe { F::from_ptr(target_ptr as *const ()) };
    let hook = match unsafe { GenericDetour::new(target, detour_fn) } {
        Ok(hook) => hook,
        Err(_) => return false,
    };
    if unsafe { hook.enable() }.is_err() {
        return false;
    }
    let Ok(mut guard) = slot.lock() else {
        return false;
    };
    *guard = Some(hook);
    true
}

fn uninstall_hook<F: Function>(slot: &Mutex<Option<GenericDetour<F>>>) -> bool {
    let Ok(mut guard) = slot.lock() else {
        return false;
    };
    match guard.take() {
        Some(hook) => unsafe { hook.disable() }.is_ok(),
        None => true,
    }
}

fn call_original<F: Function, R>(
    slot: &Mutex<Option<GenericDetour<F>>>,
    f: impl FnOnce(&GenericDetour<F>) -> R,
) -> Option<R> {
    let guard = slot.lock().ok()?;
    let hook = guard.as_ref()?;
    Some(f(hook))
}

fn with_app<R>(context: *mut ID3D11DeviceContext, f: impl FnOnce(&mut AppState) -> R) -> Option<R> {
    let mut guard = APP.lock().ok()?;
    let app = guard.as_mut()?;
    if app.device_context != context || !app.hooks_enabled {
        return None;
    }
    Some(f(app))
}

fn run_hook<R>(
    context: *mut ID3D11DeviceContext,
    hooked: impl FnOnce(&mut AppState, &dyn Fn() -> R) -> R,
    unhooked: impl Fn() -> R,
) -> R {
    with_app(context, |app| hooked(app, &unhooked)).unwrap_or_else(unhooked)
}

fn with_eye_view(
    context: *mut ID3D11DeviceContext,
    kind: ViewKind,
    access: Access,
    view: *mut c_void,
    call: impl Fn(*mut c_void),
) {
    run_hook(
        context,
        |app, _unhooked| call(eye_view(app, kind, access, view)),
        || call(view),
    );
}

fn with_eye_views(
    context: *mut ID3D11DeviceContext,
    kind: ViewKind,
    access: Access,
    views: *const *mut c_void,
    num_views: u32,
    call: impl Fn(*const *mut c_void),
) {
    run_hook(
        context,
        |app, _unhooked| {
            let substituted = eye_views(app, kind, access, views, num_views);
            call(substituted.as_ref().map_or(views, |views| views.as_ptr()));
        },
        || call(views),
    );
}

const SHADER_RESOURCE_SLOT_COUNT: usize = D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT as usize;
const UNORDERED_ACCESS_SLOT_COUNT: usize = D3D11_PS_CS_UAV_REGISTER_COUNT as usize;
const RENDER_TARGET_SLOT_COUNT: usize = D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT as usize;

fn view_slice<'a>(views: *const *mut c_void, num_views: u32) -> Option<&'a [*mut c_void]> {
    if views.is_null() || num_views == 0 {
        return None;
    }
    Some(unsafe { std::slice::from_raw_parts(views, num_views as usize) })
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
    copy_resource::original(context, created.mirror.as_raw(), created.original.as_raw());
}

fn eye_views(
    app: &mut AppState,
    kind: ViewKind,
    access: Access,
    views: *const *mut c_void,
    num_views: u32,
) -> Option<Vec<*mut c_void>> {
    if !app.camera.has_active_eye() {
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

fn eye_view(app: &mut AppState, kind: ViewKind, access: Access, view: *mut c_void) -> *mut c_void {
    eye_views(app, kind, access, &view, 1).map_or(view, |views| views[0])
}

fn eye_resource(app: &mut AppState, access: Access, resource: *mut c_void) -> *mut c_void {
    if !app.camera.has_active_eye() {
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

type SetShaderResources = fn(*mut ID3D11DeviceContext, u32, u32, *const *mut c_void) -> Option<()>;
type ShaderResourcesGetter = fn(&ID3D11DeviceContext, &mut [Option<ID3D11ShaderResourceView>]);

fn shader_resource_stages() -> [(SetShaderResources, ShaderResourcesGetter); 6] {
    [
        (vs_set_shader_resources::original, |context, views| unsafe {
            context.VSGetShaderResources(0, Some(views))
        }),
        (hs_set_shader_resources::original, |context, views| unsafe {
            context.HSGetShaderResources(0, Some(views))
        }),
        (ds_set_shader_resources::original, |context, views| unsafe {
            context.DSGetShaderResources(0, Some(views))
        }),
        (gs_set_shader_resources::original, |context, views| unsafe {
            context.GSGetShaderResources(0, Some(views))
        }),
        (ps_set_shader_resources::original, |context, views| unsafe {
            context.PSGetShaderResources(0, Some(views))
        }),
        (cs_set_shader_resources::original, |context, views| unsafe {
            context.CSGetShaderResources(0, Some(views))
        }),
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

    for (set_shader_resources, get) in shader_resource_stages() {
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
            set_shader_resources(context_ptr, slot as u32, 1, &swapped);
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
        cs_set_unordered_access_views::original(
            context_ptr,
            slot as u32,
            1,
            &swapped,
            &keep_counter,
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
    om_set_render_targets_and_unordered_access_views::original(
        context_ptr,
        count as u32,
        rtv_ptrs.as_ptr(),
        dsv_ptr,
        0,
        D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
        std::ptr::null(),
        std::ptr::null(),
    );
}
