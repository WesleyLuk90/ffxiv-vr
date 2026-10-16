use super::buffer_updates::{FOOTPRINT_INSTANCES_SIZE, upload};
use super::*;
use crate::modifiers::footprint_instances::{FootprintInstances, modify_footprint_instances};
use windows::Win32::Graphics::Direct3D11::ID3D11Buffer;

const FOOTPRINT_VERTEX_BUFFER_SLOTS: usize = 2;

fn correct_bound_footprints(app: &mut AppState) {
    if !app.camera.has_active_eye() {
        return;
    }
    let raw_context = app.device_context as *mut c_void;
    let Some(context) = (unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }) else {
        return;
    };
    let mut buffers: [Option<ID3D11Buffer>; FOOTPRINT_VERTEX_BUFFER_SLOTS] = Default::default();
    unsafe {
        context.IAGetVertexBuffers(
            0,
            FOOTPRINT_VERTEX_BUFFER_SLOTS as u32,
            Some(buffers.as_mut_ptr()),
            None,
            None,
        )
    };
    for buffer in buffers.iter().flatten() {
        let resource = buffer.as_raw();
        if app.shadow_buffers.byte_width(resource) != Some(FOOTPRINT_INSTANCES_SIZE)
            || app.corrected_footprint_buffer == Some(resource)
        {
            continue;
        }
        app.corrected_footprint_buffer = Some(resource);
        let Some(scratch) = app.shadow_buffers.load_scratch_from_shadow(resource) else {
            continue;
        };
        let footprints = unsafe { &mut *(scratch as *mut FootprintInstances) };
        if modify_footprint_instances(footprints, &app.camera) {
            upload(
                app.device_context,
                resource,
                0,
                app.shadow_buffers.scratch(),
            );
        }
    }
}

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
            |app, _unhooked| correct_bound_footprints(app),
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
