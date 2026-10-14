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
