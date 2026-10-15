use super::*;

declare_hook!(
    dispatch,
    41,
    fn(
        context,
        thread_group_count_x: u32,
        thread_group_count_y: u32,
        thread_group_count_z: u32,
    ) {
        original(
            context,
            thread_group_count_x,
            thread_group_count_y,
            thread_group_count_z,
        );
    }
);

declare_hook!(
    dispatch_indirect,
    42,
    fn(context, buffer_for_args: *mut c_void, aligned_byte_offset_for_args: u32) {
        original(context, buffer_for_args, aligned_byte_offset_for_args);
    }
);

declare_hook!(
    draw_indexed_instanced,
    20,
    fn(
        context,
        index_count_per_instance: u32,
        instance_count: u32,
        start_index_location: u32,
        base_vertex_location: i32,
        start_instance_location: u32,
    ) {
        run_hook(
            context,
            |app, _unhooked| hook_bodies::correct_bound_footprints(app),
            || {},
        );
        original(
            context,
            index_count_per_instance,
            instance_count,
            start_index_location,
            base_vertex_location,
            start_instance_location,
        );
    }
);
