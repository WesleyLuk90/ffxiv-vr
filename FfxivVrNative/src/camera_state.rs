use crate::math::Mat4;

#[derive(Clone, Copy)]
struct EyeRemap {
    view: Mat4,
    projection_override: Option<Mat4>,
}

pub struct CameraState {
    eyes: [Option<EyeRemap>; 2],
    active_eye: Option<usize>,
    original_projection: Mat4,
    eye_projections: [Option<Mat4>; 2],
    eye_projections_prev: [Option<Mat4>; 2],
}

impl CameraState {
    pub fn new() -> Self {
        Self {
            eyes: [None; 2],
            active_eye: None,
            original_projection: Mat4::IDENTITY,
            eye_projections: [None; 2],
            eye_projections_prev: [None; 2],
        }
    }

    pub fn original_projection(&self) -> Mat4 {
        self.original_projection
    }

    pub fn set_original_projection(&mut self, projection: Mat4) {
        self.original_projection = projection;
    }

    fn active_remap(&self) -> Option<EyeRemap> {
        self.eyes[self.active_eye?]
    }

    pub fn active_eye(&self) -> Option<usize> {
        self.active_eye
    }

    pub fn has_active_eye(&self) -> bool {
        self.active_eye.is_some()
    }

    pub fn eye_projection(&self) -> Option<Mat4> {
        self.active_remap()
            .map(|remap| self.remap_projection(&remap).mul(&remap.view))
    }

    pub fn eye_screen_correction(&self) -> Option<Mat4> {
        let inverse_projection = self.original_projection.transpose().invert()?;
        Some(self.eye_projection()?.mul(&inverse_projection))
    }

    pub fn eye_rotation_projection(&self) -> Option<Mat4> {
        self.active_remap().map(|remap| {
            let mut rotation = remap.view;
            rotation.m14 = 0.0;
            rotation.m24 = 0.0;
            rotation.m34 = 0.0;
            self.remap_projection(&remap).mul(&rotation)
        })
    }

    fn remap_projection(&self, remap: &EyeRemap) -> Mat4 {
        remap
            .projection_override
            .unwrap_or(self.original_projection.transpose())
    }

    pub fn record_eye_projection(&mut self, projection: Mat4) -> Mat4 {
        let Some(eye) = self.active_eye else {
            return projection;
        };
        self.eye_projections[eye] = Some(projection);
        self.eye_projections_prev[eye].unwrap_or(projection)
    }

    pub fn end_frame(&mut self) {
        for (current, prev) in self
            .eye_projections
            .iter_mut()
            .zip(self.eye_projections_prev.iter_mut())
        {
            if let Some(projection) = current.take() {
                *prev = Some(projection);
            }
        }
    }

    pub fn update_camera(&mut self, eye: usize, view: Mat4, projection: Option<Mat4>) -> bool {
        let Some(slot) = self.eyes.get_mut(eye) else {
            return false;
        };
        *slot = Some(EyeRemap {
            view,
            projection_override: projection,
        });
        true
    }

    pub fn set_active_eye(&mut self, eye: Option<usize>) -> bool {
        if eye.is_some_and(|eye| eye >= self.eyes.len()) {
            return false;
        }
        self.active_eye = eye;
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn update_camera_rejects_out_of_range_eye() {
        let mut camera = CameraState::new();
        assert!(!camera.update_camera(2, Mat4::IDENTITY, None));
    }

    #[test]
    fn set_active_eye_rejects_out_of_range_eye() {
        let mut camera = CameraState::new();
        assert!(!camera.set_active_eye(Some(2)));
        assert!(!camera.has_active_eye());
    }

    #[test]
    fn eye_projection_is_none_without_a_camera_for_the_active_eye() {
        let mut camera = CameraState::new();
        camera.set_active_eye(Some(0));
        assert_eq!(camera.eye_projection(), None);
    }
}
