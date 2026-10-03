use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct ProjectionMatrix {
    pub matrix: Mat4,
}

impl ProjectionMatrix {
    pub fn is_screen_projection_matrix(&self) -> bool {
        let m = &self.matrix;
        approximately(m.m13, 0.0)
            && approximately(m.m23, 0.0)
            && approximately(m.m31, 0.0)
            && approximately(m.m32, 0.0)
            && approximately(m.m33, 1.0)
            && approximately(m.m34, 0.0)
            && approximately(m.m41, 0.0)
            && approximately(m.m42, 0.0)
            && approximately(m.m43, 0.0)
            && approximately(m.m44, 1.0)
            && m.m11.abs() < 0.1
            && m.m11.abs() > 1e-6
            && m.m22.abs() < 0.1
            && m.m22.abs() > 1e-6
    }
}

pub fn modify_compositing_projection_matrix(
    buffer: &mut ProjectionMatrix,
    camera: &CameraState,
) -> bool {
    if !buffer.is_screen_projection_matrix() {
        return false;
    }
    let Some(inverse_projection) = camera.original_projection().transpose().invert() else {
        return false;
    };
    let Some(eye_projection) = camera.eye_projection() else {
        return false;
    };
    buffer.matrix = eye_projection.mul(&inverse_projection).mul(&buffer.matrix);
    true
}
