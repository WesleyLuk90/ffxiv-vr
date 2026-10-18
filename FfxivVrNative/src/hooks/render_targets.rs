use super::*;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_KEEP_RENDER_TARGETS_AND_DEPTH_STENCIL, D3D11_KEEP_UNORDERED_ACCESS_VIEWS,
};

use crate::modifiers::Pass;

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
        } else if resource == app.composition_texture {
            Some(if depth_stencil_view.is_null() {
                Pass::Overlay
            } else {
                Pass::Compositing
            })
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
                app.current_pass =
                    current_pass(app, num_views, render_target_views, depth_stencil_view);
                let substituted = eye_views(
                    app,
                    ViewKind::RenderTarget,
                    Access::Write,
                    render_target_views,
                    num_views,
                );
                let effective_views = substituted.as_ref().map_or(render_target_views, |views| {
                    views.as_ptr() as *mut *mut c_void
                });
                let effective_depth_stencil_view = eye_view(
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
                            eye_views(
                                app,
                                ViewKind::RenderTarget,
                                Access::Write,
                                render_target_views,
                                num_rtvs,
                            ),
                            eye_view(
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
                    eye_views(
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
