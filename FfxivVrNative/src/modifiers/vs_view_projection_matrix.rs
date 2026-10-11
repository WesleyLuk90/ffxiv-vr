use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct VSViewProjectionMatrix {
    pub matrix: Mat4,
}

impl VSViewProjectionMatrix {
    pub fn is_view_projection_matrix(&self) -> bool {
        approximately(self.matrix.m13, 0.0)
            && approximately(self.matrix.m23, 0.0)
            && approximately(self.matrix.m33, 0.0)
            && approximately(self.matrix.m43, 0.1)
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn detects_decal_view_projection() {
        let matrix = Mat4::from_row_major(&[
            -0.719814, -0.0243803, 0.0, -0.135696, 0.04963, 0.488468, 0.0, -0.52843, -0.0852564,
            0.490192, 0.0, 0.838062, 225.565, -112.173, 0.1, -90.863,
        ]);
        assert!(VSViewProjectionMatrix { matrix }.is_view_projection_matrix());
    }

    #[test]
    fn ignores_identity_rows_with_a_stale_view_projection_tail() {
        let matrix = Mat4::from_row_major(&[
            1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 302.16962, -139.12538, 0.1,
            -90.811516,
        ]);
        assert!(!VSViewProjectionMatrix { matrix }.is_view_projection_matrix());
    }
}
