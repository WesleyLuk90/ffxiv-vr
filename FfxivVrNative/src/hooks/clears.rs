use super::*;

declare_hook!(
    clear_render_target_view,
    50,
    fn(context, render_target_view: *mut c_void, color: *const f32) {
        with_eye_view(
            context,
            ViewKind::RenderTarget,
            Access::Write,
            render_target_view,
            |view| {
                original(context, view, color);
            },
        );
    }
);

declare_hook!(
    clear_unordered_access_view_uint,
    51,
    fn(context, unordered_access_view: *mut c_void, values: *const u32) {
        with_eye_view(
            context,
            ViewKind::UnorderedAccess,
            Access::Write,
            unordered_access_view,
            |view| {
                original(context, view, values);
            },
        );
    }
);

declare_hook!(
    clear_unordered_access_view_float,
    52,
    fn(context, unordered_access_view: *mut c_void, values: *const f32) {
        with_eye_view(
            context,
            ViewKind::UnorderedAccess,
            Access::Write,
            unordered_access_view,
            |view| {
                original(context, view, values);
            },
        );
    }
);

declare_hook!(
    clear_depth_stencil_view,
    53,
    fn(context, depth_stencil_view: *mut c_void, clear_flags: u32, depth: f32, stencil: u8) {
        run_hook(
            context,
            |app, _unhooked| {
                let effective_view = hook_bodies::eye_view(
                    app,
                    ViewKind::DepthStencil,
                    Access::Write,
                    depth_stencil_view,
                );
                original(context, effective_view, clear_flags, depth, stencil);
            },
            || {
                original(context, depth_stencil_view, clear_flags, depth, stencil);
            },
        );
    }
);
