use crate::camera_state::CameraState;
use crate::math::{Mat4, Vec4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct LightParamBuffer {
    pub position: Vec4,
    pub direction: Vec4,
    pub diffuse_color: Vec4,
    pub specular_color: Vec4,
    pub attenuation: Vec4,
    pub clip_min: Vec4,
    pub clip_max: Vec4,
    pub plane_fade_scale: Vec4,
    pub shadow_tex_mask: Vec4,
    pub plane_ray_direction: Vec4,
    pub plane_invers_matrix_row0: Vec4,
    pub plane_invers_matrix_row1: Vec4,
    pub plane_invers_matrix_row2: Vec4,
    pub world_view_invers_matrix_row0: Vec4,
    pub world_view_invers_matrix_row1: Vec4,
    pub world_view_invers_matrix_row2: Vec4,
    pub light_map_matrix: Mat4,
    pub world_view_projection_matrix: Mat4,
    pub light_fade_value_static: f32,
    pub light_fade_value_dynamic: f32,
    _reserved: [u8; 120],
}

impl LightParamBuffer {
    pub fn is_world_view_projection_matrix(&self) -> bool {
        let m = &self.world_view_projection_matrix;
        approximately(m.m31, 0.0)
            && approximately(m.m32, 0.0)
            && approximately(m.m33, 0.0)
            && approximately(m.m34, 0.1)
    }
}

pub fn modify_light_param(buffer: &mut LightParamBuffer, camera: &CameraState) -> bool {
    if !buffer.is_world_view_projection_matrix() {
        return false;
    }
    let Some(inverse_projection) = camera.original_projection().transpose().invert() else {
        return false;
    };
    let Some(eye_projection) = camera.eye_projection() else {
        return false;
    };
    buffer.world_view_projection_matrix = eye_projection
        .mul(&inverse_projection)
        .mul(&buffer.world_view_projection_matrix);
    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn light_param_buffer_is_512_bytes() {
        assert_eq!(std::mem::size_of::<LightParamBuffer>(), 512);
    }

    fn buffer() -> LightParamBuffer {
        unsafe { std::mem::zeroed() }
    }

    #[test]
    fn is_world_view_projection_matrix_true_for_the_captured_signature() {
        let mut buf = buffer();
        buf.world_view_projection_matrix.m34 = 0.1;
        assert!(buf.is_world_view_projection_matrix());
    }

    #[test]
    fn is_world_view_projection_matrix_false_for_zeroed_buffer() {
        assert!(!buffer().is_world_view_projection_matrix());
    }

    #[test]
    fn apply_does_nothing_when_not_recognized() {
        let camera = CameraState::new();
        let mut buf = buffer();
        assert!(!modify_light_param(&mut buf, &camera));
    }
}
