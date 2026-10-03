use core::ffi::c_void;

use windows::Win32::Graphics::Direct3D11::ID3D11Resource;
use windows::core::Interface;

#[derive(Clone, Copy)]
pub(crate) enum CameraUpload {
    Map,
    UpdateSubresource,
}

pub(crate) struct LastCameraBuffer {
    pub(crate) resource: ID3D11Resource,
    pub(crate) subresource: u32,
    pub(crate) upload: CameraUpload,
    pub(crate) byte_width: u32,
    pub(crate) original: Vec<u8>,
}

#[derive(Default)]
pub(crate) struct LastCameraBuffers {
    pub(crate) camera_parameters: Option<LastCameraBuffer>,
    pub(crate) sun_param: Option<LastCameraBuffer>,
}

impl LastCameraBuffers {
    pub(crate) fn forget(&mut self, resource: *mut c_void) {
        for slot in [&mut self.camera_parameters, &mut self.sun_param] {
            if slot
                .as_ref()
                .is_some_and(|last| last.resource.as_raw() == resource)
            {
                *slot = None;
            }
        }
    }
}
