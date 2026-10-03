use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VSViewProjectionMatrix {
    pub matrix: Mat4,
}

impl VSViewProjectionMatrix {
    pub fn is_view_projection_matrix(&self) -> bool {
        approximately(self.matrix.m43, 0.1)
    }
}

pub fn modify_geometry_vs_view_projection_matrix(
    buffer: &mut VSViewProjectionMatrix,
    camera: &CameraState,
) -> bool {
    if !buffer.is_view_projection_matrix() {
        return false;
    }
    let Some(inverse_projection) = camera.original_projection().invert() else {
        return false;
    };
    let undone = buffer.matrix.mul(&inverse_projection);
    let modified = match camera.eye_projection() {
        Some(proj) => undone.mul(&proj.transpose()),
        None => undone.mul(&camera.original_projection()),
    };
    buffer.matrix = modified;
    true
}
