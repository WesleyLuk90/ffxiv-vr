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
    pub b_view_matrix_row0: Vec4,
    pub b_view_matrix_row1: Vec4,
    pub b_view_matrix_row2: Vec4,
    pub b_inverse_view_matrix_row0: Vec4,
    pub b_inverse_view_matrix_row1: Vec4,
    pub b_inverse_view_matrix_row2: Vec4,
    pub b_view_projection_matrix: Mat4,
    pub b_inverse_view_projection_matrix: Mat4,
    pub projection_matrix2: Mat4,
    pub inverse_projection_matrix2: Mat4,
    pub identity_check1: Mat4,
    pub identity_check2_row0: Vec4,
    pub identity_check2_row1: Vec4,
    pub identity_check2_row2: Vec4,
    pub view_inverse_matrix_raw: Mat4,
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

    pub fn recompute_derived_fields(&mut self) {
        self.a_view_projection_matrix = self.projection_matrix.mul(&self.view_matrix());
        self.a_inverse_view_projection_matrix = self
            .inverse_view_matrix()
            .mul(&self.inverse_projection_matrix);

        self.b_view_matrix_row0 = self.a_view_matrix_row0;
        self.b_view_matrix_row1 = self.a_view_matrix_row1;
        self.b_view_matrix_row2 = self.a_view_matrix_row2;
        self.b_inverse_view_matrix_row0 = self.a_inverse_view_matrix_row0;
        self.b_inverse_view_matrix_row1 = self.a_inverse_view_matrix_row1;
        self.b_inverse_view_matrix_row2 = self.a_inverse_view_matrix_row2;
        self.b_view_projection_matrix = self.a_view_projection_matrix;
        self.b_inverse_view_projection_matrix = self.a_inverse_view_projection_matrix;
        self.inverse_projection_matrix2 = self.inverse_projection_matrix;
        self.projection_matrix2 = self.projection_matrix;
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
            && is_projection_row(&self.projection_matrix2)
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
    buffer.a_main_view_to_projection_matrix = projection;
    buffer.projection_matrix = projection;
    buffer.projection_matrix2 = projection;
    if let Some(inverse_projection) = projection.invert() {
        buffer.inverse_projection_matrix = inverse_projection;
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
        projection_row(&mut buf.projection_matrix2);
        assert!(buf.is_projection_buffer());
    }

    #[test]
    fn is_projection_buffer_false_when_rows_dont_match() {
        let buf: CameraParameters = unsafe { std::mem::zeroed() };
        assert!(!buf.is_projection_buffer());
    }

    #[test]
    fn recompute_derived_fields_mirrors_block_a_into_block_b() {
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        buf.projection_matrix = Mat4::IDENTITY;
        buf.inverse_projection_matrix = Mat4::IDENTITY;
        buf.a_view_matrix_row0 = Vec4::new(1.0, 0.0, 0.0, 0.0);
        buf.a_view_matrix_row1 = Vec4::new(0.0, 1.0, 0.0, 0.0);
        buf.a_view_matrix_row2 = Vec4::new(0.0, 0.0, 1.0, 0.0);
        buf.recompute_derived_fields();
        assert_eq!(buf.b_view_matrix_row0, buf.a_view_matrix_row0);
        assert_eq!(buf.a_view_projection_matrix, Mat4::IDENTITY);
        assert_eq!(buf.b_view_projection_matrix, buf.a_view_projection_matrix);
        assert_eq!(buf.projection_matrix2, buf.projection_matrix);
    }

    fn projection_buffer() -> CameraParameters {
        let mut buf: CameraParameters = unsafe { std::mem::zeroed() };
        buf.projection_matrix.m43 = -1.0;
        buf.a_main_view_to_projection_matrix.m43 = -1.0;
        buf.projection_matrix2.m43 = -1.0;
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
