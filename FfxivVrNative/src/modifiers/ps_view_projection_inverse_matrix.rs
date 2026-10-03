use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct PSViewProjectionInverseMatrix {
    pub matrix: Mat4,
}

impl PSViewProjectionInverseMatrix {
    pub fn is_inverse_view_projection_matrix(&self) -> bool {
        approximately(self.matrix.m34, 10.0)
    }
}

pub fn modify_geometry_ps_view_projection_inverse_matrix(
    buffer: &mut PSViewProjectionInverseMatrix,
    camera: &CameraState,
) -> bool {
    if !buffer.is_inverse_view_projection_matrix() {
        return false;
    }
    let original_projection = camera.original_projection();
    let Some(inverse_projection) = original_projection.invert() else {
        return false;
    };
    let undone = original_projection.mul(&buffer.matrix);
    let modified = match camera.eye_projection() {
        Some(proj) => {
            let Some(inverse_eye_projection) = proj.transpose().invert() else {
                return false;
            };
            inverse_eye_projection.mul(&undone)
        }
        None => inverse_projection.mul(&undone),
    };
    buffer.matrix = modified;
    true
}
