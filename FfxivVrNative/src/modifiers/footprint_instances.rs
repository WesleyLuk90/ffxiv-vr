use crate::camera_state::CameraState;
use crate::math::{Mat4, approximately};

pub const FOOTPRINT_INSTANCE_CAPACITY: usize = 200;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct FootprintInstance {
    pub world_view_projection: Mat4,
    pub clip_to_decal: Mat4,
    pub uv_transform: [u16; 4],
    pub fade: [u16; 4],
}

impl FootprintInstance {
    pub fn is_footprint(&self) -> bool {
        let wvp = &self.world_view_projection;
        let inverse = &self.clip_to_decal;
        approximately(wvp.m31, 0.0)
            && approximately(wvp.m32, 0.0)
            && approximately(wvp.m33, 0.0)
            && approximately(wvp.m34, 0.1)
            && approximately(inverse.m41, 0.0)
            && approximately(inverse.m42, 0.0)
            && approximately(inverse.m43, 10.0)
            && approximately(inverse.m44, 0.0)
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct FootprintInstances {
    pub instances: [FootprintInstance; FOOTPRINT_INSTANCE_CAPACITY],
}

pub fn modify_footprint_instances(buffer: &mut FootprintInstances, camera: &CameraState) -> bool {
    let Some(correction) = camera.eye_screen_correction() else {
        return false;
    };
    let Some(inverse_correction) = correction.invert() else {
        return false;
    };
    let mut modified = false;
    for instance in buffer.instances.iter_mut().filter(|i| i.is_footprint()) {
        instance.world_view_projection = correction.mul(&instance.world_view_projection);
        instance.clip_to_decal = instance.clip_to_decal.mul(&inverse_correction);
        modified = true;
    }
    modified
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};

    fn projection() -> Mat4 {
        Mat4::from_row_major(&[
            1.293417, 0.0, 0.0, 0.0, 0.0, 2.432765, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, -1.0,
            0.0,
        ])
    }

    fn footprint() -> FootprintInstance {
        FootprintInstance {
            world_view_projection: Mat4::from_row_major(&[
                0.322408, 0.0, 0.024717, 10.446838, 0.023384, 0.910457, -0.305023, -11.383396, 0.0,
                0.0, 0.0, 0.1, 0.016516, -0.217804, -0.21544, 9.932129,
            ]),
            clip_to_decal: Mat4::from_row_major(&[
                3.083536,
                0.063217,
                -341.182343,
                0.264259,
                0.0,
                0.820462,
                208.769882,
                -1.161619,
                0.236392,
                -0.824616,
                223.799789,
                -3.447041,
                0.0,
                0.0,
                10.0,
                0.0,
            ]),
            uv_transform: [0; 4],
            fade: [0; 4],
        }
    }

    fn unused() -> FootprintInstance {
        FootprintInstance {
            world_view_projection: Mat4::IDENTITY,
            clip_to_decal: Mat4::IDENTITY,
            uv_transform: [0; 4],
            fade: [0; 4],
        }
    }

    fn instances() -> Box<FootprintInstances> {
        let mut buffer = Box::new(FootprintInstances {
            instances: [unused(); FOOTPRINT_INSTANCE_CAPACITY],
        });
        buffer.instances[0] = footprint();
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

    fn assert_close(actual: &Mat4, expected: &Mat4, tolerance: f32) {
        for (a, e) in rows(actual).iter().zip(rows(expected).iter()) {
            assert!((a - e).abs() < tolerance, "{a} != {e}");
        }
    }

    #[test]
    fn instance_matches_capture_layout_size() {
        assert_eq!(std::mem::size_of::<FootprintInstance>(), 144);
        assert_eq!(std::mem::size_of::<FootprintInstances>(), 28800);
    }

    #[test]
    fn detects_footprint() {
        assert!(footprint().is_footprint());
        assert!(!unused().is_footprint());
    }

    #[test]
    fn identity_eye_view_leaves_footprint_unchanged() {
        let camera = armed_camera(Mat4::IDENTITY);
        let mut buffer = instances();
        assert!(modify_footprint_instances(&mut buffer, &camera));
        assert_close(
            &buffer.instances[0].world_view_projection,
            &footprint().world_view_projection,
            1e-3,
        );
        assert_close(
            &buffer.instances[0].clip_to_decal,
            &footprint().clip_to_decal,
            1e-1,
        );
    }

    #[test]
    fn eye_offset_keeps_matrices_inverse_of_each_other() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = instances();
        assert!(modify_footprint_instances(&mut buffer, &camera));
        let instance = &buffer.instances[0];
        assert_ne!(
            instance.world_view_projection.m14,
            footprint().world_view_projection.m14
        );
        assert_close(
            &instance.clip_to_decal.mul(&instance.world_view_projection),
            &Mat4::IDENTITY,
            1e-2,
        );
    }

    #[test]
    fn leaves_unused_instances_alone() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut buffer = instances();
        modify_footprint_instances(&mut buffer, &camera);
        assert_close(
            &buffer.instances[1].world_view_projection,
            &Mat4::IDENTITY,
            1e-6,
        );
    }
}
