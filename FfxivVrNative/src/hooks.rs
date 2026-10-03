use core::ffi::c_void;
use retour::{Function, GenericDetour};
use std::sync::Mutex;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL, D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
    ID3D11DeviceContext,
};

use crate::eye_resources::{Access, ViewKind};
use crate::hook_bodies;
use crate::{APP, AppState};

pub(crate) unsafe fn vtable_entry(
    context: *mut ID3D11DeviceContext,
    index: usize,
) -> *const c_void {
    unsafe {
        let vtable = *(context as *const *const *const c_void);
        *vtable.add(index)
    }
}

pub(crate) unsafe fn install_hook<F: Function>(
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

pub(crate) fn uninstall_hook<F: Function>(slot: &Mutex<Option<GenericDetour<F>>>) -> bool {
    let Ok(mut guard) = slot.lock() else {
        return false;
    };
    match guard.take() {
        Some(hook) => unsafe { hook.disable() }.is_ok(),
        None => true,
    }
}

pub(crate) fn call_original<F: Function, R>(
    slot: &Mutex<Option<GenericDetour<F>>>,
    f: impl FnOnce(&GenericDetour<F>) -> R,
) -> Option<R> {
    let guard = slot.lock().ok()?;
    let hook = guard.as_ref()?;
    Some(f(hook))
}

macro_rules! declare_hook {
    ($module:ident, $index:expr, fn($($arg:ident : $arg_ty:ty),* $(,)?) $(-> $ret:ty)?) => {
        pub(crate) mod $module {
            use super::*;

            pub type Fn = unsafe extern "system" fn($($arg_ty),*) $(-> $ret)?;
            pub static DETOUR: Mutex<Option<GenericDetour<Fn>>> = Mutex::new(None);
            pub const INDEX: usize = $index;
        }
    };
}

declare_hook!(
    om_set_render_targets,
    33,
    fn(
        context: *mut ID3D11DeviceContext,
        num_views: u32,
        render_target_views: *mut *mut c_void,
        depth_stencil_view: *mut c_void,
    )
);

declare_hook!(
    map,
    14,
    fn(
        context: *mut ID3D11DeviceContext,
        resource: *mut c_void,
        subresource: u32,
        map_type: u32,
        map_flags: u32,
        mapped_resource: *mut c_void,
    ) -> i32
);

declare_hook!(
    unmap,
    15,
    fn(context: *mut ID3D11DeviceContext, resource: *mut c_void, subresource: u32)
);

declare_hook!(
    update_subresource,
    48,
    fn(
        context: *mut ID3D11DeviceContext,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        dst_box: *const c_void,
        src_data: *const c_void,
        src_row_pitch: u32,
        src_depth_pitch: u32,
    )
);

declare_hook!(
    dispatch,
    41,
    fn(
        context: *mut ID3D11DeviceContext,
        thread_group_count_x: u32,
        thread_group_count_y: u32,
        thread_group_count_z: u32,
    )
);

declare_hook!(
    dispatch_indirect,
    42,
    fn(
        context: *mut ID3D11DeviceContext,
        buffer_for_args: *mut c_void,
        aligned_byte_offset_for_args: u32,
    )
);

declare_hook!(
    ps_set_shader_resources,
    8,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    cs_set_shader_resources,
    67,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    cs_set_unordered_access_views,
    68,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_uavs: u32,
        unordered_access_views: *const *mut c_void,
        uav_initial_counts: *const u32,
    )
);

declare_hook!(
    om_set_render_targets_and_unordered_access_views,
    34,
    fn(
        context: *mut ID3D11DeviceContext,
        num_rtvs: u32,
        render_target_views: *const *mut c_void,
        depth_stencil_view: *mut c_void,
        uav_start_slot: u32,
        num_uavs: u32,
        unordered_access_views: *const *mut c_void,
        uav_initial_counts: *const u32,
    )
);

declare_hook!(
    vs_set_shader_resources,
    25,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    gs_set_shader_resources,
    31,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    hs_set_shader_resources,
    59,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    ds_set_shader_resources,
    63,
    fn(
        context: *mut ID3D11DeviceContext,
        start_slot: u32,
        num_views: u32,
        shader_resource_views: *const *mut c_void,
    )
);

declare_hook!(
    copy_subresource_region,
    46,
    fn(
        context: *mut ID3D11DeviceContext,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        dst_x: u32,
        dst_y: u32,
        dst_z: u32,
        src_resource: *mut c_void,
        src_subresource: u32,
        src_box: *const c_void,
    )
);

declare_hook!(
    copy_resource,
    47,
    fn(context: *mut ID3D11DeviceContext, dst_resource: *mut c_void, src_resource: *mut c_void)
);

declare_hook!(
    clear_render_target_view,
    50,
    fn(context: *mut ID3D11DeviceContext, render_target_view: *mut c_void, color: *const f32)
);

declare_hook!(
    clear_unordered_access_view_uint,
    51,
    fn(context: *mut ID3D11DeviceContext, unordered_access_view: *mut c_void, values: *const u32)
);

declare_hook!(
    clear_unordered_access_view_float,
    52,
    fn(context: *mut ID3D11DeviceContext, unordered_access_view: *mut c_void, values: *const f32)
);

declare_hook!(
    clear_depth_stencil_view,
    53,
    fn(
        context: *mut ID3D11DeviceContext,
        depth_stencil_view: *mut c_void,
        clear_flags: u32,
        depth: f32,
        stencil: u8,
    )
);

declare_hook!(
    generate_mips,
    54,
    fn(context: *mut ID3D11DeviceContext, shader_resource_view: *mut c_void)
);

declare_hook!(
    resolve_subresource,
    57,
    fn(
        context: *mut ID3D11DeviceContext,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        src_resource: *mut c_void,
        src_subresource: u32,
        format: u32,
    )
);

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

pub(crate) unsafe extern "system" fn om_set_render_targets_detour(
    context: *mut ID3D11DeviceContext,
    num_views: u32,
    render_target_views: *mut *mut c_void,
    depth_stencil_view: *mut c_void,
) {
    run_hook(
        context,
        |app, _unhooked| {
            hook_bodies::om_set_render_targets(
                app,
                num_views,
                render_target_views,
                depth_stencil_view,
            );
            let substituted = hook_bodies::eye_views(
                app,
                ViewKind::RenderTarget,
                Access::Write,
                render_target_views,
                num_views,
            );
            let effective_views = substituted.as_ref().map_or(render_target_views, |views| {
                views.as_ptr() as *mut *mut c_void
            });
            let effective_depth_stencil_view = hook_bodies::eye_view(
                app,
                ViewKind::DepthStencil,
                Access::Write,
                depth_stencil_view,
            );
            call_original(&om_set_render_targets::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    num_views,
                    effective_views,
                    effective_depth_stencil_view,
                )
            });
        },
        move || {
            call_original(&om_set_render_targets::DETOUR, |hook| unsafe {
                hook.call(context, num_views, render_target_views, depth_stencil_view)
            });
        },
    );
}

pub(crate) unsafe extern "system" fn map_detour(
    context: *mut ID3D11DeviceContext,
    resource: *mut c_void,
    subresource: u32,
    map_type: u32,
    map_flags: u32,
    mapped_resource: *mut c_void,
) -> i32 {
    run_hook(
        context,
        |app, unhooked| {
            hook_bodies::map(
                app,
                resource,
                subresource,
                map_type,
                mapped_resource,
                unhooked,
            )
        },
        move || {
            call_original(&map::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    resource,
                    subresource,
                    map_type,
                    map_flags,
                    mapped_resource,
                )
            })
            .unwrap_or(-1)
        },
    )
}

pub(crate) unsafe extern "system" fn unmap_detour(
    context: *mut ID3D11DeviceContext,
    resource: *mut c_void,
    subresource: u32,
) {
    run_hook(
        context,
        |app, unhooked| hook_bodies::unmap(app, context, resource, subresource, unhooked),
        move || {
            call_original(&unmap::DETOUR, |hook| unsafe {
                hook.call(context, resource, subresource)
            });
        },
    );
}

pub(crate) unsafe extern "system" fn update_subresource_detour(
    context: *mut ID3D11DeviceContext,
    dst_resource: *mut c_void,
    dst_subresource: u32,
    dst_box: *const c_void,
    src_data: *const c_void,
    src_row_pitch: u32,
    src_depth_pitch: u32,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let modified = hook_bodies::compute_camera_update_subresource(
                app,
                dst_resource,
                dst_box,
                src_data,
            );
            let effective_src = modified.map_or(src_data, |bytes| bytes.as_ptr() as *const c_void);
            let effective_dst = hook_bodies::eye_resource(app, Access::Write, dst_resource);
            call_original(&update_subresource::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    effective_dst,
                    dst_subresource,
                    dst_box,
                    effective_src,
                    src_row_pitch,
                    src_depth_pitch,
                )
            });
        },
        move || {
            call_original(&update_subresource::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    dst_resource,
                    dst_subresource,
                    dst_box,
                    src_data,
                    src_row_pitch,
                    src_depth_pitch,
                )
            });
        },
    );
}

pub(crate) unsafe extern "system" fn dispatch_detour(
    context: *mut ID3D11DeviceContext,
    thread_group_count_x: u32,
    thread_group_count_y: u32,
    thread_group_count_z: u32,
) {
    run_hook(
        context,
        |_app, unhooked| unhooked(),
        move || {
            call_original(&dispatch::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    thread_group_count_x,
                    thread_group_count_y,
                    thread_group_count_z,
                )
            });
        },
    );
}

pub(crate) unsafe extern "system" fn dispatch_indirect_detour(
    context: *mut ID3D11DeviceContext,
    buffer_for_args: *mut c_void,
    aligned_byte_offset_for_args: u32,
) {
    run_hook(
        context,
        |_app, unhooked| unhooked(),
        move || {
            call_original(&dispatch_indirect::DETOUR, |hook| unsafe {
                hook.call(context, buffer_for_args, aligned_byte_offset_for_args)
            });
        },
    );
}

fn set_shader_resources(
    slot: &Mutex<Option<GenericDetour<ps_set_shader_resources::Fn>>>,
    context: *mut ID3D11DeviceContext,
    start_slot: u32,
    num_views: u32,
    shader_resource_views: *const *mut c_void,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let substituted = hook_bodies::eye_views(
                app,
                ViewKind::ShaderResource,
                Access::Read,
                shader_resource_views,
                num_views,
            );
            let effective_views = substituted
                .as_ref()
                .map_or(shader_resource_views, |views| views.as_ptr());
            call_original(slot, |hook| unsafe {
                hook.call(context, start_slot, num_views, effective_views)
            });
        },
        move || {
            call_original(slot, |hook| unsafe {
                hook.call(context, start_slot, num_views, shader_resource_views)
            });
        },
    );
}

macro_rules! set_shader_resources_detour {
    ($detour:ident, $module:ident) => {
        pub(crate) unsafe extern "system" fn $detour(
            context: *mut ID3D11DeviceContext,
            start_slot: u32,
            num_views: u32,
            shader_resource_views: *const *mut c_void,
        ) {
            set_shader_resources(
                &$module::DETOUR,
                context,
                start_slot,
                num_views,
                shader_resource_views,
            );
        }
    };
}

set_shader_resources_detour!(ps_set_shader_resources_detour, ps_set_shader_resources);
set_shader_resources_detour!(cs_set_shader_resources_detour, cs_set_shader_resources);
set_shader_resources_detour!(vs_set_shader_resources_detour, vs_set_shader_resources);
set_shader_resources_detour!(gs_set_shader_resources_detour, gs_set_shader_resources);
set_shader_resources_detour!(hs_set_shader_resources_detour, hs_set_shader_resources);
set_shader_resources_detour!(ds_set_shader_resources_detour, ds_set_shader_resources);

pub(crate) unsafe extern "system" fn cs_set_unordered_access_views_detour(
    context: *mut ID3D11DeviceContext,
    start_slot: u32,
    num_uavs: u32,
    unordered_access_views: *const *mut c_void,
    uav_initial_counts: *const u32,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let substituted = hook_bodies::eye_views(
                app,
                ViewKind::UnorderedAccess,
                Access::Write,
                unordered_access_views,
                num_uavs,
            );
            let effective_views = substituted
                .as_ref()
                .map_or(unordered_access_views, |views| views.as_ptr());
            call_original(&cs_set_unordered_access_views::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    start_slot,
                    num_uavs,
                    effective_views,
                    uav_initial_counts,
                )
            });
        },
        move || {
            call_original(&cs_set_unordered_access_views::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    start_slot,
                    num_uavs,
                    unordered_access_views,
                    uav_initial_counts,
                )
            });
        },
    );
}

pub(crate) unsafe extern "system" fn om_set_render_targets_and_unordered_access_views_detour(
    context: *mut ID3D11DeviceContext,
    num_rtvs: u32,
    render_target_views: *const *mut c_void,
    depth_stencil_view: *mut c_void,
    uav_start_slot: u32,
    num_uavs: u32,
    unordered_access_views: *const *mut c_void,
    uav_initial_counts: *const u32,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let (substituted_rtvs, effective_depth_stencil_view) =
                if num_rtvs == D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL {
                    (None, depth_stencil_view)
                } else {
                    (
                        hook_bodies::eye_views(
                            app,
                            ViewKind::RenderTarget,
                            Access::Write,
                            render_target_views,
                            num_rtvs,
                        ),
                        hook_bodies::eye_view(
                            app,
                            ViewKind::DepthStencil,
                            Access::Write,
                            depth_stencil_view,
                        ),
                    )
                };
            let substituted_uavs = if num_uavs == D3D11_KEEP_UNORDERED_ACCESS_VIEWS {
                None
            } else {
                hook_bodies::eye_views(
                    app,
                    ViewKind::UnorderedAccess,
                    Access::Write,
                    unordered_access_views,
                    num_uavs,
                )
            };
            let effective_rtvs = substituted_rtvs
                .as_ref()
                .map_or(render_target_views, |views| views.as_ptr());
            let effective_uavs = substituted_uavs
                .as_ref()
                .map_or(unordered_access_views, |views| views.as_ptr());
            call_original(
                &om_set_render_targets_and_unordered_access_views::DETOUR,
                |hook| unsafe {
                    hook.call(
                        context,
                        num_rtvs,
                        effective_rtvs,
                        effective_depth_stencil_view,
                        uav_start_slot,
                        num_uavs,
                        effective_uavs,
                        uav_initial_counts,
                    )
                },
            );
        },
        move || {
            call_original(
                &om_set_render_targets_and_unordered_access_views::DETOUR,
                |hook| unsafe {
                    hook.call(
                        context,
                        num_rtvs,
                        render_target_views,
                        depth_stencil_view,
                        uav_start_slot,
                        num_uavs,
                        unordered_access_views,
                        uav_initial_counts,
                    )
                },
            );
        },
    );
}

pub(crate) unsafe extern "system" fn copy_subresource_region_detour(
    context: *mut ID3D11DeviceContext,
    dst_resource: *mut c_void,
    dst_subresource: u32,
    dst_x: u32,
    dst_y: u32,
    dst_z: u32,
    src_resource: *mut c_void,
    src_subresource: u32,
    src_box: *const c_void,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_src = hook_bodies::eye_resource(app, Access::Read, src_resource);
            let effective_dst = hook_bodies::eye_resource(app, Access::Write, dst_resource);
            call_original(&copy_subresource_region::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    effective_dst,
                    dst_subresource,
                    dst_x,
                    dst_y,
                    dst_z,
                    effective_src,
                    src_subresource,
                    src_box,
                )
            });
        },
        move || {
            call_original(&copy_subresource_region::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    dst_resource,
                    dst_subresource,
                    dst_x,
                    dst_y,
                    dst_z,
                    src_resource,
                    src_subresource,
                    src_box,
                )
            });
        },
    );
}

pub(crate) unsafe extern "system" fn copy_resource_detour(
    context: *mut ID3D11DeviceContext,
    dst_resource: *mut c_void,
    src_resource: *mut c_void,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_src = hook_bodies::eye_resource(app, Access::Read, src_resource);
            let effective_dst = hook_bodies::eye_resource(app, Access::Write, dst_resource);
            call_original(&copy_resource::DETOUR, |hook| unsafe {
                hook.call(context, effective_dst, effective_src)
            });
        },
        move || {
            call_original(&copy_resource::DETOUR, |hook| unsafe {
                hook.call(context, dst_resource, src_resource)
            });
        },
    );
}

pub(crate) unsafe extern "system" fn resolve_subresource_detour(
    context: *mut ID3D11DeviceContext,
    dst_resource: *mut c_void,
    dst_subresource: u32,
    src_resource: *mut c_void,
    src_subresource: u32,
    format: u32,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_src = hook_bodies::eye_resource(app, Access::Read, src_resource);
            let effective_dst = hook_bodies::eye_resource(app, Access::Write, dst_resource);
            call_original(&resolve_subresource::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    effective_dst,
                    dst_subresource,
                    effective_src,
                    src_subresource,
                    format,
                )
            });
        },
        move || {
            call_original(&resolve_subresource::DETOUR, |hook| unsafe {
                hook.call(
                    context,
                    dst_resource,
                    dst_subresource,
                    src_resource,
                    src_subresource,
                    format,
                )
            });
        },
    );
}

macro_rules! clear_view_detour {
    ($detour:ident, $module:ident, $kind:expr, $value_ty:ty) => {
        pub(crate) unsafe extern "system" fn $detour(
            context: *mut ID3D11DeviceContext,
            view: *mut c_void,
            values: *const $value_ty,
        ) {
            run_hook(
                context,
                |app, _unhooked| {
                    let effective_view = hook_bodies::eye_view(app, $kind, Access::Write, view);
                    call_original(&$module::DETOUR, |hook| unsafe {
                        hook.call(context, effective_view, values)
                    });
                },
                move || {
                    call_original(&$module::DETOUR, |hook| unsafe {
                        hook.call(context, view, values)
                    });
                },
            );
        }
    };
}

clear_view_detour!(
    clear_render_target_view_detour,
    clear_render_target_view,
    ViewKind::RenderTarget,
    f32
);
clear_view_detour!(
    clear_unordered_access_view_uint_detour,
    clear_unordered_access_view_uint,
    ViewKind::UnorderedAccess,
    u32
);
clear_view_detour!(
    clear_unordered_access_view_float_detour,
    clear_unordered_access_view_float,
    ViewKind::UnorderedAccess,
    f32
);

pub(crate) unsafe extern "system" fn clear_depth_stencil_view_detour(
    context: *mut ID3D11DeviceContext,
    depth_stencil_view: *mut c_void,
    clear_flags: u32,
    depth: f32,
    stencil: u8,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_view = hook_bodies::eye_view(
                app,
                ViewKind::DepthStencil,
                Access::Write,
                depth_stencil_view,
            );
            call_original(&clear_depth_stencil_view::DETOUR, |hook| unsafe {
                hook.call(context, effective_view, clear_flags, depth, stencil)
            });
        },
        move || {
            call_original(&clear_depth_stencil_view::DETOUR, |hook| unsafe {
                hook.call(context, depth_stencil_view, clear_flags, depth, stencil)
            });
        },
    );
}

pub(crate) unsafe extern "system" fn generate_mips_detour(
    context: *mut ID3D11DeviceContext,
    shader_resource_view: *mut c_void,
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_view = hook_bodies::eye_view(
                app,
                ViewKind::ShaderResource,
                Access::Write,
                shader_resource_view,
            );
            call_original(&generate_mips::DETOUR, |hook| unsafe {
                hook.call(context, effective_view)
            });
        },
        move || {
            call_original(&generate_mips::DETOUR, |hook| unsafe {
                hook.call(context, shader_resource_view)
            });
        },
    );
}
