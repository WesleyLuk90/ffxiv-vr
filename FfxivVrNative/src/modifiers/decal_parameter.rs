use crate::camera_state::CameraState;
use crate::math::{Mat4, Vec4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct DecalParameter {
    pub world_view_matrix: [Vec4; 3],
    pub invers_world_view_matrix: [Vec4; 3],
    pub world_view_proj_matrix: Mat4,
    pub invers_world_view_proj_matrix: Mat4,
    pub param: Vec4,
    pub material0: Vec4,
    pub material1: Vec4,
    _reserved: [u8; 240],
}

impl DecalParameter {
    pub fn is_decal_parameter(&self) -> bool {
        let m = &self.world_view_proj_matrix;
        let inverse = &self.invers_world_view_proj_matrix;
        approximately(m.m31, 0.0)
            && approximately(m.m32, 0.0)
            && approximately(m.m33, 0.0)
            && approximately(m.m34, 0.1)
            && approximately(inverse.m44, 0.0)
            && is_identity(&m.mul(inverse))
    }
}

fn is_identity(m: &Mat4) -> bool {
    let rows = [
        [m.m11, m.m12, m.m13, m.m14],
        [m.m21, m.m22, m.m23, m.m24],
        [m.m31, m.m32, m.m33, m.m34],
        [m.m41, m.m42, m.m43, m.m44],
    ];
    rows.iter().enumerate().all(|(i, row)| {
        row.iter().enumerate().all(|(j, value)| {
            let expected = if i == j { 1.0 } else { 0.0 };
            (value - expected).abs() < 0.01
        })
    })
}

pub fn modify_decal_parameter(buffer: &mut DecalParameter, camera: &CameraState) -> bool {
    if !buffer.is_decal_parameter() {
        return false;
    }
    let Some(correction) = camera.eye_screen_correction() else {
        return false;
    };
    let Some(inverse_correction) = correction.invert() else {
        return false;
    };
    buffer.world_view_proj_matrix = correction.mul(&buffer.world_view_proj_matrix);
    buffer.invers_world_view_proj_matrix = buffer
        .invers_world_view_proj_matrix
        .mul(&inverse_correction);
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};

    fn projection() -> Mat4 {
        Mat4::from_row_major(&[
            1.2, 0.0, 0.0, 0.0, 0.0, 2.1, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, -1.0, 0.0,
        ])
    }

    fn world_view_proj() -> Mat4 {
        Mat4::from_row_major(&[
            2.462073, -0.793668, 0.0, 4.5284, -0.237652, -0.73723, -4.803478, 3.526583, 0.0, 0.0,
            0.0, 0.1, -0.605795, -1.879265, 0.318398, 10.899963,
        ])
    }

    fn decal() -> DecalParameter {
        let mut buffer: DecalParameter = unsafe { std::mem::zeroed() };
        buffer.world_view_proj_matrix = world_view_proj();
        buffer.invers_world_view_proj_matrix = world_view_proj().invert().unwrap();
        buffer
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

    fn rows(m: &Mat4) -> [f32; 16] {
        [
            m.m11, m.m12, m.m13, m.m14, m.m21, m.m22, m.m23, m.m24, m.m31, m.m32, m.m33, m.m34,
            m.m41, m.m42, m.m43, m.m44,
        ]
    }

    #[test]
    fn decal_parameter_is_512_bytes() {
        assert_eq!(std::mem::size_of::<DecalParameter>(), 512);
    }

    #[test]
    fn detects_captured_decal() {
        assert!(decal().is_decal_parameter());
        let zeroed: DecalParameter = unsafe { std::mem::zeroed() };
        assert!(!zeroed.is_decal_parameter());
    }

    #[test]
    fn rejects_light_param_with_stale_signature() {
        let mut light: DecalParameter = unsafe { std::mem::zeroed() };
        light.world_view_proj_matrix.m34 = 0.1;
        assert!(!light.is_decal_parameter());
    }

    #[test]
    fn eye_offset_keeps_matrices_inverse() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = decal();
        assert!(modify_decal_parameter(&mut buffer, &camera));
        assert_ne!(buffer.world_view_proj_matrix.m14, world_view_proj().m14);
        let product = buffer
            .world_view_proj_matrix
            .mul(&buffer.invers_world_view_proj_matrix);
        for (actual, expected) in rows(&product).iter().zip(rows(&Mat4::IDENTITY).iter()) {
            assert!((actual - expected).abs() < 1e-3, "{actual} != {expected}");
        }
    }
}
