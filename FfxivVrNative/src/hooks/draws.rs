use super::buffer_updates::{FOOTPRINT_INSTANCES_SIZE, PROJECTION_MATRIX_SIZE, upload};
use super::*;
use crate::modifiers::Pass;
use crate::modifiers::footprint_instances::{FootprintInstances, modify_footprint_instances};
use crate::modifiers::projection_matrix::{
    ProjectionMatrix, modify_target_cursor_projection_matrix,
};
use crate::target_cursor::TargetCursorDraw;
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

struct SavedOutputMerger<'a> {
    context: &'a ID3D11DeviceContext,
    render_target_views: [Option<ID3D11RenderTargetView>; RENDER_TARGET_SLOT_COUNT],
    depth_stencil_view: Option<ID3D11DepthStencilView>,
    depth_stencil_state: Option<ID3D11DepthStencilState>,
    stencil_ref: u32,
}

impl<'a> SavedOutputMerger<'a> {
    fn capture(context: &'a ID3D11DeviceContext) -> Self {
        let mut saved = Self {
            context,
            render_target_views: Default::default(),
            depth_stencil_view: None,
            depth_stencil_state: None,
            stencil_ref: 0,
        };
        unsafe {
            context.OMGetRenderTargets(
                Some(&mut saved.render_target_views),
                Some(&mut saved.depth_stencil_view),
            )
        };
        unsafe {
            context.OMGetDepthStencilState(
                Some(&mut saved.depth_stencil_state),
                Some(&mut saved.stencil_ref),
            )
        };
        saved
    }
}

impl Drop for SavedOutputMerger<'_> {
    fn drop(&mut self) {
        unsafe {
            self.context
                .OMSetDepthStencilState(self.depth_stencil_state.as_ref(), self.stencil_ref)
        };
        let mut views = self.render_target_views.each_ref().map(|view| {
            view.as_ref()
                .map_or(std::ptr::null_mut(), Interface::as_raw)
        });
        om_set_render_targets::original(
            self.context.as_raw().cast(),
            RENDER_TARGET_SLOT_COUNT as u32,
            views.as_mut_ptr(),
            self.depth_stencil_view
                .as_ref()
                .map_or(std::ptr::null_mut(), Interface::as_raw),
        );
    }
}

fn bound_projection_buffer(
    app: &mut AppState,
    context: &ID3D11DeviceContext,
    texture: *mut c_void,
) -> Option<*mut c_void> {
    let mut textures: [Option<ID3D11ShaderResourceView>; 1] = Default::default();
    unsafe { context.PSGetShaderResources(0, Some(&mut textures)) };
    if textures[0].as_ref()?.as_raw() != texture {
        return None;
    }
    let mut buffers: [Option<ID3D11Buffer>; 1] = Default::default();
    unsafe { context.VSGetConstantBuffers(0, Some(&mut buffers)) };
    let projection_buffer = buffers[0].as_ref()?.as_raw();
    (app.shadow_buffers.byte_width(projection_buffer) == Some(PROJECTION_MATRIX_SIZE))
        .then_some(projection_buffer)
}

fn draw_target_cursor(app: &mut AppState, draw: impl FnOnce()) -> Option<()> {
    if app.current_pass != Some(Pass::Overlay) {
        return None;
    }
    let cursor = app.target_cursor.draw_for_eye(app.camera.active_eye()?)?;
    let raw_context = app.device_context as *mut c_void;
    let context = unsafe { ID3D11DeviceContext::from_raw_borrowed(&raw_context) }?;
    let projection_buffer = bound_projection_buffer(app, context, cursor.texture)?;
    let scratch = app
        .shadow_buffers
        .load_scratch_from_shadow(projection_buffer)?;
    let projection = unsafe { &mut *(scratch as *mut ProjectionMatrix) };
    modify_target_cursor_projection_matrix(projection, &app.camera, cursor.depth).then_some(())?;
    upload(
        app.device_context,
        projection_buffer,
        0,
        app.shadow_buffers.scratch(),
    );
    draw_into(context, &cursor, draw);
    if let Some(original) = app.shadow_buffers.data(projection_buffer) {
        upload(app.device_context, projection_buffer, 0, original);
    }
    Some(())
}

fn draw_into(context: &ID3D11DeviceContext, cursor: &TargetCursorDraw, draw: impl FnOnce()) {
    let _saved = SavedOutputMerger::capture(context);
    let mut eye_views = [cursor.eye_target.as_raw()];
    om_set_render_targets::original(
        context.as_raw().cast(),
        1,
        eye_views.as_mut_ptr(),
        cursor
            .depth_test
            .as_ref()
            .map_or(std::ptr::null_mut(), |depth_test| depth_test.view.as_raw()),
    );
    if let Some(depth_test) = &cursor.depth_test {
        unsafe { context.OMSetDepthStencilState(&depth_test.state, 0) };
    }
    draw();
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
                if draw_target_cursor(app, draw).is_none() {
                    unhooked();
                }
            },
            draw,
        );
    }
);
