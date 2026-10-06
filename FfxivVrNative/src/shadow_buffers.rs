use core::ffi::c_void;
use std::collections::HashMap;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_BUFFER_DESC, D3D11_RESOURCE_DIMENSION_BUFFER, ID3D11Buffer, ID3D11Resource,
};
use windows::core::Interface;

struct ShadowBuffer {
    data: Vec<u8>,
    pending_subresource: Option<u32>,
}

#[derive(Default)]
pub(crate) struct ShadowBuffers {
    buffers: HashMap<*mut c_void, ShadowBuffer>,
    byte_widths: HashMap<*mut c_void, Option<u32>>,
    update_subresource_scratch: Vec<u8>,
}

impl ShadowBuffers {
    pub(crate) fn byte_width(&mut self, resource: *mut c_void) -> Option<u32> {
        if resource.is_null() {
            return None;
        }
        *self
            .byte_widths
            .entry(resource)
            .or_insert_with(|| buffer_byte_width(resource))
    }

    pub(crate) fn begin_map(
        &mut self,
        resource: *mut c_void,
        subresource: u32,
        byte_width: u32,
    ) -> *mut c_void {
        let shadow = self
            .buffers
            .entry(resource)
            .or_insert_with(|| ShadowBuffer {
                data: vec![0u8; byte_width as usize],
                pending_subresource: None,
            });
        if shadow.data.len() != byte_width as usize {
            shadow.data.resize(byte_width as usize, 0);
        }
        shadow.pending_subresource = Some(subresource);
        shadow.data.as_mut_ptr() as *mut c_void
    }

    pub(crate) fn take_pending(
        &mut self,
        resource: *mut c_void,
        subresource: u32,
    ) -> Option<(*mut c_void, u32)> {
        let shadow = self.buffers.get_mut(&resource)?;
        if shadow.pending_subresource.take() != Some(subresource) {
            return None;
        }
        Some((
            shadow.data.as_mut_ptr() as *mut c_void,
            shadow.data.len() as u32,
        ))
    }

    pub(crate) fn data(&self, resource: *mut c_void) -> Option<&[u8]> {
        self.buffers
            .get(&resource)
            .map(|shadow| shadow.data.as_slice())
    }

    pub(crate) fn load_update_subresource_scratch(&mut self, data: &[u8]) -> *mut c_void {
        self.update_subresource_scratch.clear();
        self.update_subresource_scratch.extend_from_slice(data);
        self.update_subresource_scratch.as_mut_ptr() as *mut c_void
    }

    pub(crate) fn update_subresource_scratch(&self) -> &[u8] {
        &self.update_subresource_scratch
    }
}

fn buffer_byte_width(resource: *mut c_void) -> Option<u32> {
    let base = unsafe { ID3D11Resource::from_raw_borrowed(&resource) }?;
    if unsafe { base.GetType() } != D3D11_RESOURCE_DIMENSION_BUFFER {
        return None;
    }
    let typed = unsafe { ID3D11Buffer::from_raw_borrowed(&resource) }?;
    let mut desc = D3D11_BUFFER_DESC::default();
    unsafe { typed.GetDesc(&mut desc) };
    Some(desc.ByteWidth)
}
