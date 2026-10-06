use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct ClipToWorldMatrix {
    pub matrix: Mat4,
}

impl ClipToWorldMatrix {
    pub fn is_clip_to_world_matrix(&self) -> bool {
        approximately(self.matrix.m41, 0.0)
            && approximately(self.matrix.m42, 0.0)
            && approximately(self.matrix.m43, 10.0)
            && approximately(self.matrix.m44, 0.0)
    }
}

pub fn modify_clip_to_world_matrix(buffer: &mut ClipToWorldMatrix, camera: &CameraState) -> bool {
    if !buffer.is_clip_to_world_matrix() {
        return false;
    }
    let Some(inverse_correction) = camera
        .eye_screen_correction()
        .and_then(|correction| correction.invert())
    else {
        return false;
    };
    buffer.matrix = buffer.matrix.mul(&inverse_correction);
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};
    use crate::modifiers::ps_view_projection_inverse_matrix::PSViewProjectionInverseMatrix;
    use crate::modifiers::vs_view_projection_matrix::VSViewProjectionMatrix;
    use crate::modifiers::world_view_proj_matrix::WorldViewProjMatrix;

    fn projection() -> Mat4 {
        Mat4::from_row_major(&[
            1.293417, 0.0, 0.0, 0.0, 0.0, 2.432765, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, -1.0,
            0.0,
        ])
    }

    fn fog_clip_to_world() -> Mat4 {
        Mat4::from_row_major(&[
            -0.709383,
            0.025299,
            -654.485962,
            0.39288,
            0.0,
            0.406102,
            160.232101,
            -0.154766,
            0.307458,
            0.058371,
            -1814.154297,
            0.906473,
            0.0,
            0.0,
            10.0,
            0.0,
        ])
    }

    fn armed_camera(eye_view: Mat4) -> CameraState {
        let mut camera = CameraState::new();
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        buf.projection_matrix = projection();
        buf.a_main_view_to_projection_matrix = projection();
        buf.projection_matrix2 = projection();
        modify_camera_parameters(&mut buf, &mut camera);
        camera.update_camera(0, eye_view, None);
        camera.set_active_eye(Some(0));
        camera
    }

    fn assert_close(actual: &Mat4, expected: &Mat4) {
        let rows = |m: &Mat4| {
            [
                m.m11, m.m12, m.m13, m.m14, m.m21, m.m22, m.m23, m.m24, m.m31, m.m32, m.m33, m.m34,
                m.m41, m.m42, m.m43, m.m44,
            ]
        };
        for (a, e) in rows(actual).iter().zip(rows(expected).iter()) {
            assert!((a - e).abs() < 1e-2, "{a} != {e}");
        }
    }

    #[test]
    fn detects_fog_clip_to_world_only() {
        let matrix = fog_clip_to_world();
        assert!(ClipToWorldMatrix { matrix }.is_clip_to_world_matrix());
        assert!(!PSViewProjectionInverseMatrix { matrix }.is_inverse_view_projection_matrix());
        assert!(!VSViewProjectionMatrix { matrix }.is_view_projection_matrix());
        assert!(!WorldViewProjMatrix { matrix }.is_view_projection_matrix());
    }

    #[test]
    fn does_nothing_without_an_active_eye() {
        let mut camera = armed_camera(Mat4::IDENTITY);
        camera.set_active_eye(None);
        let mut buffer = ClipToWorldMatrix {
            matrix: fog_clip_to_world(),
        };
        assert!(!modify_clip_to_world_matrix(&mut buffer, &camera));
    }

    #[test]
    fn identity_eye_view_leaves_matrix_unchanged() {
        let camera = armed_camera(Mat4::IDENTITY);
        let mut buffer = ClipToWorldMatrix {
            matrix: fog_clip_to_world(),
        };
        assert!(modify_clip_to_world_matrix(&mut buffer, &camera));
        assert_close(&buffer.matrix, &fog_clip_to_world());
    }

    #[test]
    fn inverts_the_eye_projection() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = ClipToWorldMatrix {
            matrix: fog_clip_to_world(),
        };
        assert!(modify_clip_to_world_matrix(&mut buffer, &camera));
        let eye_projection = camera.eye_projection().unwrap();
        let inverse_view = fog_clip_to_world().mul(&projection());
        assert_close(&buffer.matrix.mul(&eye_projection), &inverse_view);
        assert!((buffer.matrix.m13 - fog_clip_to_world().m13).abs() > 1e-2);
    }
}
