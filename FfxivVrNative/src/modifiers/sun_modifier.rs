use crate::camera_state::CameraState;
use crate::math::{Vec4, approximately};
use crate::modifiers::sky_quad_param::reproject_uv;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct SunParam {
    pub aspect_position: Vec4,
    pub shape: Vec4,
    pub falloff: Vec4,
    pub halo_color: Vec4,
    pub color: Vec4,
    pub unused: [Vec4; 3],
}

impl SunParam {
    pub fn is_sun(&self, camera: &CameraState) -> bool {
        let projection = camera.original_projection();
        if projection.m11.abs() < 1e-6 {
            return false;
        }
        let aspect = projection.m22 / projection.m11;
        approximately(self.aspect_position.x, aspect) && self.aspect_position.y == 1.0
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct RadialBlurParam {
    pub center_strength: Vec4,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct LensFlareParam {
    pub position: Vec4,
    pub color: Vec4,
    pub ring: Vec4,
    pub pixel_scale: Vec4,
}

#[derive(Default)]
pub struct SunModifier {
    sun_position: Option<(f32, f32)>,
}

impl SunModifier {
    pub fn is_tracking_sun(&self) -> bool {
        self.sun_position.is_some()
    }

    pub fn reset(&mut self) {
        self.sun_position = None;
    }

    pub fn modify_sun_param(&mut self, buffer: &mut SunParam, camera: &CameraState) -> bool {
        if !buffer.is_sun(camera) {
            return false;
        }
        let position = (buffer.aspect_position.z, buffer.aspect_position.w);
        let Some((u, v)) = eye_position(camera, position) else {
            return false;
        };
        buffer.aspect_position.z = u;
        buffer.aspect_position.w = v;
        self.sun_position = Some(position);
        true
    }

    pub fn modify_radial_blur_param(
        &self,
        buffer: &mut RadialBlurParam,
        camera: &CameraState,
    ) -> bool {
        let center = &mut buffer.center_strength;
        if self.sun_position != Some((center.x, center.y)) {
            return false;
        }
        let Some((u, v)) = eye_position(camera, (center.x, center.y)) else {
            return false;
        };
        center.x = u;
        center.y = v;
        true
    }

    pub fn modify_lens_flare_param(
        &self,
        buffer: &mut LensFlareParam,
        camera: &CameraState,
    ) -> bool {
        let Some(sun_position) = self.sun_position else {
            return false;
        };
        let position = &mut buffer.position;
        let (x, y) = uv_to_ndc(sun_position);
        if !approximately(position.x, x) || !approximately(position.y, y) {
            return false;
        }
        let Some(eye) = eye_position(camera, sun_position) else {
            return false;
        };
        (position.x, position.y) = uv_to_ndc(eye);
        true
    }
}

fn uv_to_ndc((u, v): (f32, f32)) -> (f32, f32) {
    (2.0 * u - 1.0, 1.0 - 2.0 * v)
}

fn eye_position(camera: &CameraState, (u, v): (f32, f32)) -> Option<(f32, f32)> {
    reproject_uv(&camera.eye_screen_correction()?, u, v, 0.0)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::math::Mat4;
    use crate::modifiers::camera_parameters::{CameraParameters, modify_camera_parameters};

    fn projection() -> Mat4 {
        Mat4::from_row_major(&[
            1.293418, 0.0, 0.0, 0.0, 0.0, 2.432767, 0.0, 0.0, 0.0, 0.0, 0.0, 0.1, 0.0, 0.0, -1.0,
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

    fn rotated_eye_view() -> Mat4 {
        let angle: f32 = 0.05;
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m11 = angle.cos();
        eye_view.m13 = angle.sin();
        eye_view.m31 = -angle.sin();
        eye_view.m33 = angle.cos();
        eye_view
    }

    fn sun() -> SunParam {
        SunParam {
            aspect_position: Vec4::new(1.880882, 1.0, 0.617181, 0.321017),
            shape: Vec4::new(3.0, 1.001611, 4.884521, 0.494192),
            falloff: Vec4::new(-43.776825, -60.839924, -84.553787, -380.249542),
            halo_color: Vec4::new(1.414214, 1.0, 0.707107, 1.0),
            color: Vec4::new(1.3134, 1.314146, 1.313367, 0.1),
            unused: [Vec4::default(); 3],
        }
    }

    fn radial_blur(x: f32, y: f32) -> RadialBlurParam {
        RadialBlurParam {
            center_strength: Vec4::new(x, y, 0.0, 0.8),
        }
    }

    #[test]
    fn sun_param_is_128_bytes() {
        assert_eq!(std::mem::size_of::<SunParam>(), 128);
    }

    #[test]
    fn radial_blur_param_is_16_bytes() {
        assert_eq!(std::mem::size_of::<RadialBlurParam>(), 16);
    }

    #[test]
    fn recognizes_sun_buffer() {
        let camera = armed_camera(Mat4::IDENTITY);
        assert!(sun().is_sun(&camera));
    }

    #[test]
    fn rejects_other_aspect() {
        let camera = armed_camera(Mat4::IDENTITY);
        let mut buffer = sun();
        buffer.aspect_position.x = 1.5;
        assert!(!buffer.is_sun(&camera));
    }

    #[test]
    fn identity_eye_view_leaves_sun_unchanged() {
        let camera = armed_camera(Mat4::IDENTITY);
        let mut modifier = SunModifier::default();
        let mut buffer = sun();
        assert!(modifier.modify_sun_param(&mut buffer, &camera));
        assert!(approximately(buffer.aspect_position.z, 0.617181));
        assert!(approximately(buffer.aspect_position.w, 0.321017));
    }

    #[test]
    fn eye_translation_does_not_move_sun() {
        let mut eye_view = Mat4::IDENTITY;
        eye_view.m14 = 0.032;
        let camera = armed_camera(eye_view);
        let mut modifier = SunModifier::default();
        let mut buffer = sun();
        assert!(modifier.modify_sun_param(&mut buffer, &camera));
        assert!(approximately(buffer.aspect_position.z, 0.617181));
        assert!(approximately(buffer.aspect_position.w, 0.321017));
    }

    #[test]
    fn radial_blur_follows_sun() {
        let camera = armed_camera(rotated_eye_view());
        let mut modifier = SunModifier::default();
        let mut buffer = sun();
        assert!(modifier.modify_sun_param(&mut buffer, &camera));
        assert!(!approximately(buffer.aspect_position.z, 0.617181));
        assert!(modifier.is_tracking_sun());
        let mut blur = radial_blur(0.617181, 0.321017);
        assert!(modifier.modify_radial_blur_param(&mut blur, &camera));
        assert_eq!(blur.center_strength.x, buffer.aspect_position.z);
        assert_eq!(blur.center_strength.y, buffer.aspect_position.w);
        assert_eq!(blur.center_strength.w, 0.8);
    }

    #[test]
    fn radial_blur_ignores_other_centers() {
        let camera = armed_camera(rotated_eye_view());
        let mut modifier = SunModifier::default();
        assert!(modifier.modify_sun_param(&mut sun(), &camera));
        let mut blur = radial_blur(0.5, 0.321017);
        assert!(!modifier.modify_radial_blur_param(&mut blur, &camera));
    }

    fn lens_flare(x: f32, y: f32) -> LensFlareParam {
        LensFlareParam {
            position: Vec4::new(x, y, 14.676072, 0.279996),
            color: Vec4::new(0.105072, 0.105132, 0.105069, 1.0),
            ring: Vec4::new(0.0, 0.999988, 0.000016, 0.2),
            pixel_scale: Vec4::new(0.000782, 0.001471, 0.0, 0.0),
        }
    }

    #[test]
    fn lens_flare_param_is_64_bytes() {
        assert_eq!(std::mem::size_of::<LensFlareParam>(), 64);
    }

    #[test]
    fn lens_flare_follows_sun() {
        let camera = armed_camera(rotated_eye_view());
        let mut modifier = SunModifier::default();
        let mut buffer = sun();
        assert!(modifier.modify_sun_param(&mut buffer, &camera));
        let mut flare = lens_flare(0.234363, 0.357965);
        assert!(modifier.modify_lens_flare_param(&mut flare, &camera));
        let (x, y) = uv_to_ndc((buffer.aspect_position.z, buffer.aspect_position.w));
        assert!(approximately(flare.position.x, x));
        assert!(approximately(flare.position.y, y));
        assert_eq!(flare.position.z, 14.676072);
    }

    #[test]
    fn lens_flare_ignores_other_positions() {
        let camera = armed_camera(rotated_eye_view());
        let mut modifier = SunModifier::default();
        assert!(modifier.modify_sun_param(&mut sun(), &camera));
        let mut flare = lens_flare(-0.5, 0.357965);
        assert!(!modifier.modify_lens_flare_param(&mut flare, &camera));
    }

    #[test]
    fn reset_forgets_sun() {
        let camera = armed_camera(rotated_eye_view());
        let mut modifier = SunModifier::default();
        assert!(modifier.modify_sun_param(&mut sun(), &camera));
        modifier.reset();
        assert!(!modifier.is_tracking_sun());
        let mut blur = radial_blur(0.617181, 0.321017);
        assert!(!modifier.modify_radial_blur_param(&mut blur, &camera));
    }
}
