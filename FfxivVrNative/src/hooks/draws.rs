use super::buffer_updates::{FOOTPRINT_INSTANCES_SIZE, upload};
use super::*;
use crate::billboards::{BillboardConstants, BillboardInputs};
use crate::modifiers::Pass;
use crate::modifiers::footprint_instances::{FootprintInstances, modify_footprint_instances};
use crate::modifiers::projection_matrix::ProjectionMatrix;
use windows::Win32::Graphics::Direct3D11::ID3D11Buffer;

const FOOTPRINT_VERTEX_BUFFER_SLOTS: usize = 2;
const QUAD_INDEX_COUNT: u32 = 6;
const NAMEPLATE_VERTEX_STRIDE: u32 = 40;

fn bound_screen_projection(
    app: &AppState,
    context: &ID3D11DeviceContext,
) -> Option<(*mut c_void, ProjectionMatrix)> {
    let mut buffers: [Option<ID3D11Buffer>; 1] = Default::default();
    unsafe { context.VSGetConstantBuffers(0, Some(&mut buffers)) };
    let resource = buffers[0].as_ref()?.as_raw();
    let data = app.shadow_buffers.data(resource)?;
    if data.len() != std::mem::size_of::<ProjectionMatrix>() {
        return None;
    }
    let projection = unsafe { std::ptr::read_unaligned(data.as_ptr() as *const ProjectionMatrix) };
    projection
        .is_screen_projection_matrix()
        .then_some((resource, projection))
}

fn has_nameplate_vertices(context: &ID3D11DeviceContext) -> bool {
    let mut buffers: [Option<ID3D11Buffer>; 1] = Default::default();
    let mut strides = [0u32; 1];
    unsafe {
        context.IAGetVertexBuffers(
            0,
            1,
            Some(buffers.as_mut_ptr()),
            Some(strides.as_mut_ptr()),
            None,
        )
    };
    buffers[0].is_some() && strides[0] == NAMEPLATE_VERTEX_STRIDE
}

fn has_geometry_shader(context: &ID3D11DeviceContext) -> bool {
    let mut shader = None;
    unsafe { context.GSGetShader(&mut shader, None, None) };
    shader.is_some()
}

type GeometryConstants = [Option<ID3D11Buffer>; 1];

fn bind_billboard(app: &mut AppState, index_count: u32) -> Option<GeometryConstants> {
    if index_count % QUAD_INDEX_COUNT != 0
        || app.current_pass != Some(Pass::Compositing)
        || !app.camera.has_active_eye()
    {
        return None;
    }
    let raw_context = app.device_context as *mut c_void;
    let context = unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }?;
    let (_, screen) = bound_screen_projection(app, context)?;
    if !has_nameplate_vertices(context) || has_geometry_shader(context) {
        return None;
    }
    let inputs = BillboardInputs {
        projection: app.camera.eye_base_projection()?,
        clip_from_screen: app.camera.eye_screen_correction()?.mul(&screen.matrix),
        view_from_world: app.camera.eye_view_from_world()?,
        screen_up: app.camera.eye_screen_up()?,
        world_up: app.camera.eye_world_up()?,
    };
    let billboards = app.billboards.as_mut()?;
    if billboards.uploaded != Some(inputs) {
        let constants = BillboardConstants::new(&inputs, &app.nameplate_anchors)?;
        upload(
            app.device_context,
            billboards.constants.as_raw(),
            0,
            constants.as_bytes(),
        );
        billboards.uploaded = Some(inputs);
    }
    let mut saved = GeometryConstants::default();
    unsafe {
        context.GSGetConstantBuffers(0, Some(&mut saved));
        context.GSSetShader(&billboards.shader, None);
        context.GSSetConstantBuffers(0, Some(&[Some(billboards.constants.clone())]));
    }
    Some(saved)
}

fn unbind_billboard(context: *mut ID3D11DeviceContext, saved: GeometryConstants) {
    let raw_context = context as *mut c_void;
    let Some(context) = (unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }) else {
        return;
    };
    unsafe {
        context.GSSetShader(None, None);
        context.GSSetConstantBuffers(0, Some(&saved));
    }
}

declare_hook!(
    draw_indexed,
    12,
    fn(context, index_count: u32, start_index_location: u32, base_vertex_location: i32) {
        let saved = run_hook(
            context,
            |app, _unhooked| bind_billboard(app, index_count),
            || None,
        );
        original(context, index_count, start_index_location, base_vertex_location);
        if let Some(saved) = saved {
            unbind_billboard(context, saved);
        }
    }
);

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
