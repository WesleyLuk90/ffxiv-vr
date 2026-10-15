use super::*;

fn with_eye_resources(
    context: *mut ID3D11DeviceContext,
    dst_resource: *mut c_void,
    src_resource: *mut c_void,
    call: impl Fn(*mut c_void, *mut c_void),
) {
    run_hook(
        context,
        |app, _unhooked| {
            let effective_src = eye_resource(app, Access::Read, src_resource);
            let effective_dst = eye_resource(app, Access::Write, dst_resource);
            call(effective_dst, effective_src);
        },
        || call(dst_resource, src_resource),
    );
}

declare_hook!(
    copy_subresource_region,
    46,
    fn(
        context,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        dst_x: u32,
        dst_y: u32,
        dst_z: u32,
        src_resource: *mut c_void,
        src_subresource: u32,
        src_box: *const c_void,
    ) {
        with_eye_resources(context, dst_resource, src_resource, |dst, src| {
            original(
                context,
                dst,
                dst_subresource,
                dst_x,
                dst_y,
                dst_z,
                src,
                src_subresource,
                src_box,
            );
        });
    }
);

declare_hook!(
    copy_resource,
    47,
    fn(context, dst_resource: *mut c_void, src_resource: *mut c_void) {
        with_eye_resources(context, dst_resource, src_resource, |dst, src| {
            original(context, dst, src);
        });
    }
);

declare_hook!(
    resolve_subresource,
    57,
    fn(
        context,
        dst_resource: *mut c_void,
        dst_subresource: u32,
        src_resource: *mut c_void,
        src_subresource: u32,
        format: u32,
    ) {
        with_eye_resources(context, dst_resource, src_resource, |dst, src| {
            original(context, dst, dst_subresource, src, src_subresource, format);
        });
    }
);
