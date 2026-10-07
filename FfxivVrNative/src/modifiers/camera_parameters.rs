use crate::camera_state::CameraState;
use crate::math::{Mat4, Vec4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CameraParameters {
    pub a_view_matrix_row0: Vec4,
    pub a_view_matrix_row1: Vec4,
    pub a_view_matrix_row2: Vec4,
    pub a_inverse_view_matrix_row0: Vec4,
    pub a_inverse_view_matrix_row1: Vec4,
    pub a_inverse_view_matrix_row2: Vec4,
    pub a_view_projection_matrix: Mat4,
    pub a_inverse_view_projection_matrix: Mat4,
    pub inverse_projection_matrix: Mat4,
    pub projection_matrix: Mat4,
    pub a_main_view_to_projection_matrix: Mat4,
    pub view_matrix_prev_row0: Vec4,
    pub view_matrix_prev_row1: Vec4,
    pub view_matrix_prev_row2: Vec4,
    pub inverse_view_matrix_prev_row0: Vec4,
    pub inverse_view_matrix_prev_row1: Vec4,
    pub inverse_view_matrix_prev_row2: Vec4,
    pub view_projection_matrix_prev: Mat4,
    pub inverse_view_projection_matrix_prev: Mat4,
    pub projection_matrix_prev: Mat4,
    pub inverse_projection_matrix_prev: Mat4,
    pub proj_to_proj_prev_matrix: Mat4,
    pub view_to_view_prev_matrix_row0: Vec4,
    pub view_to_view_prev_matrix_row1: Vec4,
    pub view_to_view_prev_matrix_row2: Vec4,
    pub main_view_to_world_matrix_row0: Vec4,
    pub main_view_to_world_matrix_row1: Vec4,
    pub main_view_to_world_matrix_row2: Vec4,
    pub jitter_velocity: Vec4,
    pub padding0: Vec4,
    pub padding1: Vec4,
    pub padding2: Vec4,
    pub padding3: Vec4,
    pub padding4: Vec4,
}

impl CameraParameters {
    pub fn view_matrix(&self) -> Mat4 {
        Mat4::from_rows3(
            self.a_view_matrix_row0,
            self.a_view_matrix_row1,
            self.a_view_matrix_row2,
        )
    }

    pub fn inverse_view_matrix(&self) -> Mat4 {
        Mat4::from_rows3(
            self.a_inverse_view_matrix_row0,
            self.a_inverse_view_matrix_row1,
            self.a_inverse_view_matrix_row2,
        )
    }

    pub fn view_matrix_prev(&self) -> Mat4 {
        Mat4::from_rows3(
            self.view_matrix_prev_row0,
            self.view_matrix_prev_row1,
            self.view_matrix_prev_row2,
        )
    }

    pub fn inverse_view_matrix_prev(&self) -> Mat4 {
        Mat4::from_rows3(
            self.inverse_view_matrix_prev_row0,
            self.inverse_view_matrix_prev_row1,
            self.inverse_view_matrix_prev_row2,
        )
    }

    pub fn recompute_derived_fields(&mut self) {
        self.a_view_projection_matrix = self.projection_matrix.mul(&self.view_matrix());
        self.a_inverse_view_projection_matrix = self
            .inverse_view_matrix()
            .mul(&self.inverse_projection_matrix);
        self.view_projection_matrix_prev =
            self.projection_matrix_prev.mul(&self.view_matrix_prev());
        self.inverse_view_projection_matrix_prev = self
            .inverse_view_matrix_prev()
            .mul(&self.inverse_projection_matrix_prev);
        self.proj_to_proj_prev_matrix = self
            .projection_matrix_prev
            .mul(&self.view_matrix_prev())
            .mul(&self.inverse_view_matrix())
            .mul(&self.inverse_projection_matrix);
    }

    pub fn is_projection_buffer(&self) -> bool {
        fn is_projection_row(m: &Mat4) -> bool {
            approximately(m.m41, 0.0)
                && approximately(m.m42, 0.0)
                && approximately(m.m43, -1.0)
                && approximately(m.m44, 0.0)
        }
        is_projection_row(&self.projection_matrix)
            && is_projection_row(&self.a_main_view_to_projection_matrix)
            && is_projection_row(&self.projection_matrix_prev)
    }
}

pub fn modify_camera_parameters(buffer: &mut CameraParameters, camera: &mut CameraState) -> bool {
    if !buffer.is_projection_buffer() {
        return false;
    }
    camera.set_original_projection(buffer.a_main_view_to_projection_matrix.transpose());
    let Some(projection) = camera.eye_projection() else {
        return false;
    };
    let projection_prev = camera.record_eye_projection(projection);
    buffer.a_main_view_to_projection_matrix = projection;
    buffer.projection_matrix = projection;
    buffer.projection_matrix_prev = projection_prev;
    if let Some(inverse_projection) = projection.invert() {
        buffer.inverse_projection_matrix = inverse_projection;
    }
    if let Some(inverse_projection_prev) = projection_prev.invert() {
        buffer.inverse_projection_matrix_prev = inverse_projection_prev;
    }
    buffer.recompute_derived_fields();
    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn camera_parameters_is_1024_bytes() {
        assert_eq!(std::mem::size_of::<CameraParameters>(), 1024);
    }

    #[test]
    fn is_projection_buffer_true_for_well_formed_projection_rows() {
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        let projection_row = |m: &mut Mat4| {
            m.m43 = -1.0;
        };
        projection_row(&mut buf.projection_matrix);
        projection_row(&mut buf.a_main_view_to_projection_matrix);
        projection_row(&mut buf.projection_matrix_prev);
        assert!(buf.is_projection_buffer());
    }

    #[test]
    fn is_projection_buffer_false_when_rows_dont_match() {
        let buf: CameraParameters = unsafe { std::mem::zeroed() };
        assert!(!buf.is_projection_buffer());
    }

    fn translation(x: f32) -> Mat4 {
        Mat4::from_rows3(
            Vec4::new(1.0, 0.0, 0.0, x),
            Vec4::new(0.0, 1.0, 0.0, 0.0),
            Vec4::new(0.0, 0.0, 1.0, 0.0),
        )
    }

    fn perspective() -> Mat4 {
        Mat4::from_row_major(&[
            1.3, 0.0, 0.0, 0.0, //
            0.0, 2.4, 0.0, 0.0, //
            0.0, 0.0, 0.0, 0.1, //
            0.0, 0.0, -1.0, 0.0,
        ])
    }

    fn set_view(buf: &mut CameraParameters, view: Mat4, view_prev: Mat4) {
        let inverse = view.invert().unwrap();
        let inverse_prev = view_prev.invert().unwrap();
        let rows = |m: Mat4| {
            [
                Vec4::new(m.m11, m.m12, m.m13, m.m14),
                Vec4::new(m.m21, m.m22, m.m23, m.m24),
                Vec4::new(m.m31, m.m32, m.m33, m.m34),
            ]
        };
        [
            buf.a_view_matrix_row0,
            buf.a_view_matrix_row1,
            buf.a_view_matrix_row2,
        ] = rows(view);
        [
            buf.a_inverse_view_matrix_row0,
            buf.a_inverse_view_matrix_row1,
            buf.a_inverse_view_matrix_row2,
        ] = rows(inverse);
        [
            buf.view_matrix_prev_row0,
            buf.view_matrix_prev_row1,
            buf.view_matrix_prev_row2,
        ] = rows(view_prev);
        [
            buf.inverse_view_matrix_prev_row0,
            buf.inverse_view_matrix_prev_row1,
            buf.inverse_view_matrix_prev_row2,
        ] = rows(inverse_prev);
    }

    fn assert_approx(a: Vec4, b: Vec4) {
        for (x, y) in [(a.x, b.x), (a.y, b.y), (a.z, b.z), (a.w, b.w)] {
            assert!((x - y).abs() < 1e-4, "{a:?} vs {b:?}");
        }
    }

    #[test]
    fn proj_to_proj_prev_reprojects_into_the_eye_previous_frame() {
        let mut camera = CameraState::new();
        let view = translation(1.0);
        let view_prev = translation(0.5);
        let mut buf = projection_buffer();
        buf.a_main_view_to_projection_matrix = perspective();
        buf.projection_matrix = perspective();
        buf.projection_matrix_prev = perspective();
        set_view(&mut buf, view, view_prev);

        camera.update_camera(0, translation(-0.03), None);
        camera.set_active_eye(Some(0));
        assert!(modify_camera_parameters(&mut buf.clone(), &mut camera));
        camera.end_frame();
        camera.update_camera(0, translation(-0.04), None);
        assert!(modify_camera_parameters(&mut buf, &mut camera));

        let world = Vec4::new(0.3, -0.2, -5.0, 1.0);
        let clip = buf.a_view_projection_matrix.transform(world);
        let clip_prev = buf.view_projection_matrix_prev.transform(world);
        let reprojected = buf.proj_to_proj_prev_matrix.transform(clip);
        let expected = perspective()
            .mul(&translation(-0.03))
            .mul(&view_prev)
            .transform(world);
        assert_approx(clip_prev, expected);
        assert_approx(reprojected, expected);
    }

    #[test]
    fn first_frame_uses_the_current_eye_projection_as_previous() {
        let mut camera = CameraState::new();
        let mut buf = projection_buffer();
        buf.a_main_view_to_projection_matrix = perspective();
        set_view(&mut buf, Mat4::IDENTITY, Mat4::IDENTITY);
        camera.update_camera(0, translation(-0.03), None);
        camera.set_active_eye(Some(0));
        assert!(modify_camera_parameters(&mut buf, &mut camera));
        assert_eq!(buf.projection_matrix_prev, buf.projection_matrix);
    }

    fn projection_buffer() -> CameraParameters {
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        buf.projection_matrix.m43 = -1.0;
        buf.a_main_view_to_projection_matrix.m43 = -1.0;
        buf.projection_matrix_prev.m43 = -1.0;
        buf
    }

    #[test]
    fn apply_tracks_original_projection_without_an_armed_view() {
        let mut camera = CameraState::new();
        let mut buf = projection_buffer();
        buf.a_main_view_to_projection_matrix.m11 = 42.0;
        assert!(!modify_camera_parameters(&mut buf, &mut camera));
        assert_eq!(camera.original_projection().m11, 42.0);
    }

    #[test]
    fn apply_does_nothing_for_a_non_projection_buffer() {
        let mut camera = CameraState::new();
        camera.update_camera(1, Mat4::IDENTITY, None);
        camera.set_active_eye(Some(1));
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        assert!(!modify_camera_parameters(&mut buf, &mut camera));
    }

    #[test]
    fn apply_rewrites_projection_when_armed() {
        let mut camera = CameraState::new();
        camera.update_camera(0, Mat4::IDENTITY, Some(Mat4::IDENTITY));
        camera.set_active_eye(Some(0));
        let mut buf = projection_buffer();
        assert!(modify_camera_parameters(&mut buf, &mut camera));
        assert_eq!(buf.a_main_view_to_projection_matrix, Mat4::IDENTITY);
        assert_eq!(buf.projection_matrix, Mat4::IDENTITY);
    }

    #[test]
    fn apply_does_nothing_without_an_active_eye() {
        let mut camera = CameraState::new();
        camera.update_camera(0, Mat4::IDENTITY, Some(Mat4::IDENTITY));
        let mut buf = projection_buffer();
        assert!(!modify_camera_parameters(&mut buf, &mut camera));
    }
}
