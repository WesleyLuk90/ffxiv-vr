use super::*;

macro_rules! declare_set_shader_resources {
    ($module:ident, $index:expr) => {
        declare_hook!(
            $module,
            $index,
            fn(
                context,
                start_slot: u32,
                num_views: u32,
                shader_resource_views: *const *mut c_void,
            ) {
                with_eye_views(
                    context,
                    ViewKind::ShaderResource,
                    Access::Read,
                    shader_resource_views,
                    num_views,
                    |views| {
                        original(context, start_slot, num_views, views);
                    },
                );
            }
        );
    };
}

declare_set_shader_resources!(ps_set_shader_resources, 8);
declare_set_shader_resources!(vs_set_shader_resources, 25);
declare_set_shader_resources!(gs_set_shader_resources, 31);
declare_set_shader_resources!(hs_set_shader_resources, 59);
declare_set_shader_resources!(ds_set_shader_resources, 63);
declare_set_shader_resources!(cs_set_shader_resources, 67);

declare_hook!(
    generate_mips,
    54,
    fn(context, shader_resource_view: *mut c_void) {
        with_eye_view(
            context,
            ViewKind::ShaderResource,
            Access::Write,
            shader_resource_view,
            |view| {
                original(context, view);
            },
        );
    }
);
