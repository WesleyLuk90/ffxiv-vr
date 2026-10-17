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

pub fn modify_target_cursor_projection_matrix(
    buffer: &mut ProjectionMatrix,
    camera: &CameraState,
    depth: f32,
) -> bool {
    if !buffer.is_screen_projection_matrix() {
        return false;
    }
    let Some(correction) = camera.eye_screen_correction() else {
        return false;
    };
    let mut pinned = buffer.matrix;
    pinned.m31 = 0.0;
    pinned.m32 = 0.0;
    pinned.m33 = 0.0;
    pinned.m34 = depth;
    buffer.matrix = correction.mul(&pinned);
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::math::Vec4;

    fn screen_projection() -> ProjectionMatrix {
        let mut matrix = Mat4::IDENTITY;
        matrix.m11 = 2.0 / 2558.0;
        matrix.m14 = -1.000391;
        matrix.m22 = -2.0 / 1360.0;
        matrix.m24 = 1.000735;
        ProjectionMatrix { matrix }
    }

    fn camera_with_eye(view: Mat4) -> CameraState {
        let mut camera = CameraState::new();
        camera.set_original_projection(Mat4::IDENTITY);
        camera.update_camera(0, view, Some(Mat4::IDENTITY));
        camera.set_active_eye(Some(0));
        camera
    }

    #[test]
    fn target_cursor_pins_vertex_depth() {
        let mut buffer = screen_projection();
        assert!(modify_target_cursor_projection_matrix(
            &mut buffer,
            &camera_with_eye(Mat4::IDENTITY),
            0.0115
        ));
        let clip = buffer.matrix.transform(Vec4::new(651.0, 560.0, 0.0, 1.0));
        assert!(approximately(clip.z, 0.0115));
        assert!(approximately(clip.w, 1.0));
    }

    #[test]
    fn target_cursor_follows_eye_shift() {
        let mut shift = Mat4::IDENTITY;
        shift.m14 = 0.25;
        let mut buffer = screen_projection();
        let original = buffer.matrix.transform(Vec4::new(651.0, 560.0, 0.0, 1.0));
        assert!(modify_target_cursor_projection_matrix(
            &mut buffer,
            &camera_with_eye(shift),
            0.0115
        ));
        let clip = buffer.matrix.transform(Vec4::new(651.0, 560.0, 0.0, 1.0));
        assert!(approximately(clip.x, original.x + 0.25));
        assert!(approximately(clip.y, original.y));
    }

    #[test]
    fn target_cursor_ignores_non_screen_matrix() {
        let mut buffer = ProjectionMatrix {
            matrix: Mat4::IDENTITY,
        };
        assert!(!modify_target_cursor_projection_matrix(
            &mut buffer,
            &camera_with_eye(Mat4::IDENTITY),
            0.0115
        ));
    }
}
