use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct WorldViewProjMatrix {
    pub matrix: Mat4,
}

impl WorldViewProjMatrix {
    pub fn is_view_projection_matrix(&self) -> bool {
        approximately(self.matrix.m31, 0.0)
            && approximately(self.matrix.m32, 0.0)
            && approximately(self.matrix.m33, 0.0)
            && approximately(self.matrix.m34, 0.1)
    }

    pub fn is_camera_relative(&self) -> bool {
        approximately(self.matrix.m14, 0.0)
            && approximately(self.matrix.m24, 0.0)
            && approximately(self.matrix.m44, 0.0)
    }
}

pub fn modify_world_view_proj_matrix(
    buffer: &mut WorldViewProjMatrix,
    camera: &CameraState,
) -> bool {
    if !buffer.is_view_projection_matrix() {
        return false;
    }
    let Some(inverse_projection) = camera.original_projection().transpose().invert() else {
        return false;
    };
    let eye_projection = if buffer.is_camera_relative() {
        camera.eye_rotation_projection()
    } else {
        camera.eye_projection()
    };
    let Some(eye_projection) = eye_projection else {
        return false;
    };
    buffer.matrix = eye_projection.mul(&inverse_projection).mul(&buffer.matrix);
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};
    use crate::modifiers::vs_view_projection_matrix::VSViewProjectionMatrix;

    fn projection() -> Mat4 {
        Mat4::from_row_major(&[
            1.2, 0.0, 0.0, 0.0, 0.0, 2.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, -1.0, 0.0,
        ])
    }

    fn snow_world_view_projection() -> Mat4 {
        Mat4::from_row_major(&[
            10.69014, 0.0, 7.28104, -9.69788, 4.38564, 23.04646, -6.43906, -15.81366, 0.0, 0.0,
            0.0, 0.1, 5.33285, -3.20241, -7.82977, 18.53592,
        ])
    }

    fn star_world_view_projection() -> Mat4 {
        Mat4::from_row_major(&[
            0.77302, -1.03451, -0.07191, 0.00008, 1.8585, 1.43325, -0.64041, 0.00007, 0.0, 0.0,
            0.0, 0.1, -0.2433, -0.11485, -0.96313, 0.0,
        ])
    }

    fn armed_camera(eye_view: Mat4) -> CameraState {
        let mut camera = CameraState::new();
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        buf.projection_matrix = projection();
        buf.a_main_view_to_projection_matrix = projection();
        buf.projection_matrix_prev = projection();
        modify_camera_parameters(&mut buf, &mut camera);
        camera.update_camera(0, eye_view, None);
        camera.set_active_eye(Some(0));
        camera
    }

    #[test]
    fn detects_snow_world_view_projection() {
        let buffer = WorldViewProjMatrix {
            matrix: snow_world_view_projection(),
        };
        assert!(buffer.is_view_projection_matrix());
        let decal = VSViewProjectionMatrix {
            matrix: snow_world_view_projection(),
        };
        assert!(!decal.is_view_projection_matrix());
    }

    #[test]
    fn identity_eye_view_leaves_matrix_unchanged() {
        let camera = armed_camera(Mat4::IDENTITY);
        let mut buffer = WorldViewProjMatrix {
            matrix: snow_world_view_projection(),
        };
        assert!(modify_world_view_proj_matrix(&mut buffer, &camera));
        let expected = snow_world_view_projection();
        let rows = |m: &Mat4| {
            [
                m.m11, m.m12, m.m13, m.m14, m.m21, m.m22, m.m23, m.m24, m.m31, m.m32, m.m33, m.m34,
                m.m41, m.m42, m.m43, m.m44,
            ]
        };
        for (actual, expected) in rows(&buffer.matrix).iter().zip(rows(&expected).iter()) {
            assert!((actual - expected).abs() < 1e-3, "{actual} != {expected}");
        }
    }

    #[test]
    fn applies_eye_offset() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = WorldViewProjMatrix {
            matrix: snow_world_view_projection(),
        };
        assert!(modify_world_view_proj_matrix(&mut buffer, &camera));
        assert_ne!(buffer.matrix.m14, snow_world_view_projection().m14);
        assert!((buffer.matrix.m34 - 0.1).abs() < 1e-4);
    }

    #[test]
    fn detects_camera_relative_star_matrix() {
        let stars = WorldViewProjMatrix {
            matrix: star_world_view_projection(),
        };
        assert!(stars.is_view_projection_matrix());
        assert!(stars.is_camera_relative());
        let snow = WorldViewProjMatrix {
            matrix: snow_world_view_projection(),
        };
        assert!(!snow.is_camera_relative());
    }

    #[test]
    fn camera_relative_matrix_ignores_eye_offset() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = WorldViewProjMatrix {
            matrix: star_world_view_projection(),
        };
        assert!(modify_world_view_proj_matrix(&mut buffer, &camera));
        let expected = star_world_view_projection();
        assert!((buffer.matrix.m14 - expected.m14).abs() < 1e-4);
        assert!((buffer.matrix.m24 - expected.m24).abs() < 1e-4);
        assert!((buffer.matrix.m44 - expected.m44).abs() < 1e-4);
    }
}
