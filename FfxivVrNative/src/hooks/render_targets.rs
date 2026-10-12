use super::*;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL, D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
};

declare_hook!(
    om_set_render_targets,
    33,
    fn(
        context,
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
                original(context, num_views, effective_views, effective_depth_stencil_view);
            },
            || {
                original(context, num_views, render_target_views, depth_stencil_view);
            },
        );
    }
);

declare_hook!(
    om_set_render_targets_and_unordered_access_views,
    34,
    fn(
        context,
        num_rtvs: u32,
        render_target_views: *const *mut c_void,
        depth_stencil_view: *mut c_void,
        uav_start_slot: u32,
        num_uavs: u32,
        unordered_access_views: *const *mut c_void,
        uav_initial_counts: *const u32,
    ) {
        let call = |render_target_views, depth_stencil_view, unordered_access_views| {
            original(
                context,
                num_rtvs,
                render_target_views,
                depth_stencil_view,
                uav_start_slot,
                num_uavs,
                unordered_access_views,
                uav_initial_counts,
            );
        };
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
                call(
                    substituted_rtvs
                        .as_ref()
                        .map_or(render_target_views, |views| views.as_ptr()),
                    effective_depth_stencil_view,
                    substituted_uavs
                        .as_ref()
                        .map_or(unordered_access_views, |views| views.as_ptr()),
                );
            },
            || call(render_target_views, depth_stencil_view, unordered_access_views),
        );
    }
);

declare_hook!(
    cs_set_unordered_access_views,
    68,
    fn(
        context,
        start_slot: u32,
        num_uavs: u32,
        unordered_access_views: *const *mut c_void,
        uav_initial_counts: *const u32,
    ) {
        with_eye_views(
            context,
            ViewKind::UnorderedAccess,
            Access::Write,
            unordered_access_views,
            num_uavs,
            |views| {
                original(context, start_slot, num_uavs, views, uav_initial_counts);
            },
        );
    }
);
