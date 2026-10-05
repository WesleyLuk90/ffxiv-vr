use crate::camera_state::CameraState;
use crate::math::{Mat4, Vec4, approximately};

#[repr(C)]
#[derive(Clone, Copy)]
pub struct SkyQuadParam {
    pub position_scale_offset: Vec4,
    pub disk_scale_offset: Vec4,
    pub depth: Vec4,
    pub unused: Vec4,
}

impl SkyQuadParam {
    pub fn is_sky_quad(&self) -> bool {
        let disk = &self.disk_scale_offset;
        approximately(disk.x, 2.0)
            && approximately(disk.y, -2.0)
            && approximately(disk.z, -1.0)
            && approximately(disk.w, 1.0)
            && approximately(self.depth.x, 0.0)
            && approximately(self.depth.y, 0.0)
            && approximately(self.depth.z, 1.0)
            && approximately(self.depth.w, 1.0)
    }
}

pub fn modify_sky_quad_param(buffer: &mut SkyQuadParam, camera: &CameraState) -> bool {
    if !buffer.is_sky_quad() {
        return false;
    }
    let Some(correction) = camera.eye_screen_correction() else {
        return false;
    };
    let quad = buffer.position_scale_offset;
    let depth = buffer.depth.x;
    let Some((x0, y0)) = reproject_uv(&correction, quad.z, quad.w, depth) else {
        return false;
    };
    let Some((x1, y1)) = reproject_uv(&correction, quad.z + quad.x, quad.w + quad.y, depth) else {
        return false;
    };
    buffer.position_scale_offset = Vec4::new(x1 - x0, y1 - y0, x0, y0);
    true
}

pub fn reproject_uv(correction: &Mat4, u: f32, v: f32, depth: f32) -> Option<(f32, f32)> {
    let clip = correction.transform(Vec4::new(2.0 * u - 1.0, 1.0 - 2.0 * v, depth, 1.0));
    if clip.w.abs() < 1e-6 {
        return None;
    }
    Some(((clip.x / clip.w + 1.0) * 0.5, (1.0 - clip.y / clip.w) * 0.5))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn moon() -> SkyQuadParam {
        SkyQuadParam {
            position_scale_offset: Vec4::new(0.05353, 0.10069, 0.51037, 0.11682),
            disk_scale_offset: Vec4::new(2.0, -2.0, -1.0, 1.0),
            depth: Vec4::new(0.0, 0.0, 1.0, 1.0),
            unused: Vec4::default(),
        }
    }

    #[test]
    fn sky_quad_param_is_64_bytes() {
        assert_eq!(std::mem::size_of::<SkyQuadParam>(), 64);
    }

    #[test]
    fn recognizes_moon_buffer() {
        assert!(moon().is_sky_quad());
    }

    #[test]
    fn rejects_other_depth() {
        let mut buffer = moon();
        buffer.depth.x = 0.5;
        assert!(!buffer.is_sky_quad());
    }

    #[test]
    fn reproject_uv_is_identity_for_identity_correction() {
        let (u, v) = reproject_uv(&Mat4::IDENTITY, 0.25, 0.75, 0.0).unwrap();
        assert!(approximately(u, 0.25));
        assert!(approximately(v, 0.75));
    }

    #[test]
    fn reproject_uv_follows_ndc_shift() {
        let mut shift = Mat4::IDENTITY;
        shift.m14 = 0.5;
        let (u, v) = reproject_uv(&shift, 0.25, 0.75, 0.0).unwrap();
        assert!(approximately(u, 0.5));
        assert!(approximately(v, 0.75));
    }
}
