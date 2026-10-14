use super::*;
use crate::eye_resources::Access;

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
                hook_bodies::map(app, resource, subresource, map_type, mapped_resource, unhooked)
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
            |app, unhooked| hook_bodies::unmap(app, context, resource, subresource, unhooked),
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
                    hook_bodies::compute_camera_update_subresource(app, dst_resource, dst_box, src_data);
                let effective_src =
                    modified.map_or(src_data, |bytes| bytes.as_ptr() as *const c_void);
                let effective_dst = hook_bodies::eye_resource(app, Access::Write, dst_resource);
                call(effective_dst, effective_src);
            },
            || call(dst_resource, src_data),
        );
    }
);
