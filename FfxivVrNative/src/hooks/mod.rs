use core::ffi::c_void;
use retour::{Function, GenericDetour};
use std::sync::Mutex;

use windows::Win32::Graphics::Direct3D11::ID3D11DeviceContext;

use crate::eye_resources::{Access, ViewKind};
use crate::hook_bodies;
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
pub(crate) use draws::{dispatch, dispatch_indirect};
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
            dispatch,
            dispatch_indirect,
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
        |app, _unhooked| call(hook_bodies::eye_view(app, kind, access, view)),
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
            let substituted = hook_bodies::eye_views(app, kind, access, views, num_views);
            call(substituted.as_ref().map_or(views, |views| views.as_ptr()));
        },
        || call(views),
    );
}
