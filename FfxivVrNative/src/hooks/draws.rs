use super::buffer_updates::{FOOTPRINT_INSTANCES_SIZE, PROJECTION_MATRIX_SIZE, upload};
use super::*;
use crate::modifiers::Pass;
use crate::modifiers::footprint_instances::{FootprintInstances, modify_footprint_instances};
use crate::modifiers::projection_matrix::{
    ProjectionMatrix, modify_target_cursor_projection_matrix,
};
use windows::Win32::Graphics::Direct3D11::{
    ID3D11Buffer, ID3D11DepthStencilState, ID3D11DepthStencilView,
};

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

struct TargetCursorDraw {
    eye_target: ID3D11RenderTargetView,
    eye_depth: Option<(ID3D11DepthStencilView, ID3D11DepthStencilState)>,
    projection_buffer: *mut c_void,
    depth: f32,
}

fn target_cursor_draw(app: &mut AppState) -> Option<TargetCursorDraw> {
    if app.current_pass != Some(Pass::Overlay) || app.target_cursor_texture.is_null() {
        return None;
    }
    let depth = app.target_cursor_depth?;
    let eye_scene_target = app
        .eye_scene_targets
        .as_ref()?
        .get(app.camera.active_eye()?)?;
    let eye_target = eye_scene_target.color.clone();
    let eye_depth = app
        .target_cursor_depth_state
        .as_ref()
        .filter(|_| app.target_cursor_depth_tested)
        .map(|state| (eye_scene_target.depth.clone(), state.clone()));
    let raw_context = app.device_context as *mut c_void;
    let context = unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }?;
    let mut texture: [Option<ID3D11ShaderResourceView>; 1] = Default::default();
    unsafe { context.PSGetShaderResources(0, Some(&mut texture)) };
    if texture[0].as_ref()?.as_raw() != app.target_cursor_texture {
        return None;
    }
    let mut buffer: [Option<ID3D11Buffer>; 1] = Default::default();
    unsafe { context.VSGetConstantBuffers(0, Some(&mut buffer)) };
    let projection_buffer = buffer[0].as_ref()?.as_raw();
    if app.shadow_buffers.byte_width(projection_buffer) != Some(PROJECTION_MATRIX_SIZE) {
        return None;
    }
    Some(TargetCursorDraw {
        eye_target,
        eye_depth,
        projection_buffer,
        depth,
    })
}

fn draw_target_cursor(app: &mut AppState, cursor: TargetCursorDraw, draw: impl FnOnce()) -> bool {
    let Some(scratch) = app
        .shadow_buffers
        .load_scratch_from_shadow(cursor.projection_buffer)
    else {
        return false;
    };
    let projection = unsafe { &mut *(scratch as *mut ProjectionMatrix) };
    if !modify_target_cursor_projection_matrix(projection, &app.camera, cursor.depth) {
        return false;
    }
    let context = app.device_context;
    upload(
        context,
        cursor.projection_buffer,
        0,
        app.shadow_buffers.scratch(),
    );
    draw_into(app, &cursor.eye_target, cursor.eye_depth.as_ref(), draw);
    if let Some(original) = app.shadow_buffers.data(cursor.projection_buffer) {
        upload(context, cursor.projection_buffer, 0, original);
    }
    true
}

fn draw_into(
    app: &AppState,
    eye_target: &ID3D11RenderTargetView,
    eye_depth: Option<&(ID3D11DepthStencilView, ID3D11DepthStencilState)>,
    draw: impl FnOnce(),
) {
    let raw_context = app.device_context as *mut c_void;
    let Some(context) = (unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }) else {
        return;
    };
    let mut views: [Option<ID3D11RenderTargetView>; RENDER_TARGET_SLOT_COUNT] = Default::default();
    let mut depth_stencil_view = None;
    unsafe { context.OMGetRenderTargets(Some(&mut views), Some(&mut depth_stencil_view)) };
    let mut depth_stencil_state = None;
    let mut stencil_ref = 0;
    unsafe {
        context.OMGetDepthStencilState(Some(&mut depth_stencil_state), Some(&mut stencil_ref))
    };
    let mut eye_views = [eye_target.as_raw()];
    om_set_render_targets::original(
        app.device_context,
        1,
        eye_views.as_mut_ptr(),
        eye_depth.map_or(std::ptr::null_mut(), |(view, _)| view.as_raw()),
    );
    if let Some((_, state)) = eye_depth {
        unsafe { context.OMSetDepthStencilState(state, 0) };
    }
    draw();
    if eye_depth.is_some() {
        unsafe { context.OMSetDepthStencilState(depth_stencil_state.as_ref(), stencil_ref) };
    }
    let mut restored = views.map(|view| {
        view.as_ref()
            .map_or(std::ptr::null_mut(), Interface::as_raw)
    });
    om_set_render_targets::original(
        app.device_context,
        RENDER_TARGET_SLOT_COUNT as u32,
        restored.as_mut_ptr(),
        depth_stencil_view
            .as_ref()
            .map_or(std::ptr::null_mut(), Interface::as_raw),
    );
}

declare_hook!(
    draw_indexed,
    12,
    fn(
        context,
        index_count: u32,
        start_index_location: u32,
        base_vertex_location: i32,
    ) {
        let draw = || {
            original(context, index_count, start_index_location, base_vertex_location);
        };
        run_hook(
            context,
            |app, unhooked| {
                let drawn = target_cursor_draw(app)
                    .is_some_and(|cursor| draw_target_cursor(app, cursor, draw));
                if !drawn {
                    unhooked();
                }
            },
            draw,
        );
    }
);
