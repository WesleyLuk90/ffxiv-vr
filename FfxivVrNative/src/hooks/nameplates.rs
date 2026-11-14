use super::buffer_updates::upload;
use super::*;
use crate::billboards::{BillboardConstants, BillboardInputs};
use crate::modifiers::Pass;
use crate::modifiers::projection_matrix::ProjectionMatrix;
use windows::Win32::Graphics::Direct3D11::ID3D11Buffer;

const QUAD_INDEX_COUNT: u32 = 6;
const NAMEPLATE_VERTEX_STRIDE: u32 = 40;

pub(super) type GeometryConstants = [Option<ID3D11Buffer>; 1];

pub(super) fn bound_screen_projection(
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

pub(super) fn bind_billboard(app: &mut AppState, index_count: u32) -> Option<GeometryConstants> {
    if index_count % QUAD_INDEX_COUNT != 0
        || app.current_pass != Some(Pass::Compositing)
        || !app.camera.has_active_eye()
    {
        return None;
    }
    let raw_context = app.device_context as *mut c_void;
    let context = unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }?;
    let (_, screen) = bound_screen_projection(app, context)?;
    bind_billboard_shader(app, context, &screen)
}

pub(super) fn bind_billboard_shader(
    app: &mut AppState,
    context: &ID3D11DeviceContext,
    screen: &ProjectionMatrix,
) -> Option<GeometryConstants> {
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

pub(super) fn unbind_billboard(context: *mut ID3D11DeviceContext, saved: GeometryConstants) {
    let raw_context = context as *mut c_void;
    let Some(context) = (unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }) else {
        return;
    };
    unsafe {
        context.GSSetShader(None, None);
        context.GSSetConstantBuffers(0, Some(&saved));
    }
}
