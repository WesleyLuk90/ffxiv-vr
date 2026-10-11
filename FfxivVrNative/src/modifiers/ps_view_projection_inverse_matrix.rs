use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct PSViewProjectionInverseMatrix {
    pub matrix: Mat4,
}

impl PSViewProjectionInverseMatrix {
    pub fn is_inverse_view_projection_matrix(&self) -> bool {
        approximately(self.matrix.m14, 0.0)
            && approximately(self.matrix.m24, 0.0)
            && approximately(self.matrix.m34, 10.0)
            && approximately(self.matrix.m44, 0.0)
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn detects_decal_inverse_view_projection() {
        let matrix = Mat4::from_row_major(&[
            1.37638, 0.0, 0.0, 0.0, 0.0, 1.48226, 0.0, 0.0, 0.0, 0.0, 0.0, 10.0, 0.0, -0.231229,
            -1.0, 0.0,
        ]);
        assert!(PSViewProjectionInverseMatrix { matrix }.is_inverse_view_projection_matrix());
    }

    #[test]
    fn ignores_a_matrix_with_only_the_near_plane_term() {
        let matrix = Mat4::from_row_major(&[
            1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 10.0, 0.0, 0.0, 0.0, 1.0,
        ]);
        assert!(!PSViewProjectionInverseMatrix { matrix }.is_inverse_view_projection_matrix());
    }
}
