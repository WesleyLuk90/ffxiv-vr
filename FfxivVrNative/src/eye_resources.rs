use core::ffi::c_void;
use std::collections::HashMap;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_DEPTH_STENCIL_VIEW_DESC, D3D11_RENDER_TARGET_VIEW_DESC,
    D3D11_RESOURCE_DIMENSION_TEXTURE2D, D3D11_SHADER_RESOURCE_VIEW_DESC, D3D11_TEXTURE2D_DESC,
    D3D11_UNORDERED_ACCESS_VIEW_DESC, ID3D11DepthStencilView, ID3D11Device, ID3D11RenderTargetView,
    ID3D11Resource, ID3D11ShaderResourceView, ID3D11Texture2D, ID3D11UnorderedAccessView,
    ID3D11View,
};
use windows::core::Interface;

pub(crate) const MIRRORED_EYE: usize = 1;

const MAX_UNMIRRORED_TEXELS: u64 = 16;

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum ViewKind {
    ShaderResource,
    UnorderedAccess,
    RenderTarget,
    DepthStencil,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum Access {
    Read,
    Write,
}

#[derive(Default, Clone, Copy)]
struct Usage {
    written_in_eye_pass: bool,
    written_this_pass: bool,
    read_before_write: bool,
}

#[derive(Default)]
pub(crate) struct UsageTracker {
    usage: HashMap<usize, Usage>,
}

impl UsageTracker {
    pub(crate) fn begin_pass(&mut self) {
        for usage in self.usage.values_mut() {
            usage.written_this_pass = false;
        }
    }

    pub(crate) fn record(&mut self, key: usize, access: Access) -> bool {
        let usage = self.usage.entry(key).or_default();
        match access {
            Access::Read => {
                if !usage.written_this_pass {
                    usage.read_before_write = true;
                }
            }
            Access::Write => {
                usage.written_this_pass = true;
                usage.written_in_eye_pass = true;
            }
        }
        usage.written_in_eye_pass && usage.read_before_write
    }

    pub(crate) fn forget_unwritten(&mut self) {
        self.usage.retain(|_, usage| usage.written_in_eye_pass);
    }

    pub(crate) fn remove(&mut self, key: usize) {
        self.usage.remove(&key);
    }
}

struct Mirror {
    resource: ID3D11Resource,
    views: HashMap<usize, ID3D11View>,
}

pub(crate) struct NewMirror {
    pub(crate) original: ID3D11Resource,
    pub(crate) mirror: ID3D11Resource,
}

#[derive(Default)]
pub(crate) struct EyeResources {
    tracker: UsageTracker,
    mirrors: HashMap<usize, Option<Mirror>>,
    live_mirror_count: usize,
    originals_by_mirror_view: HashMap<usize, usize>,
}

impl EyeResources {
    pub(crate) fn begin_pass(&mut self) {
        self.tracker.begin_pass();
    }

    pub(crate) fn end_frame(&mut self) {
        self.tracker.forget_unwritten();
    }

    pub(crate) fn has_mirrors(&self) -> bool {
        self.live_mirror_count > 0
    }

    pub(crate) fn record(
        &mut self,
        device: &ID3D11Device,
        resource: &ID3D11Resource,
        access: Access,
        excluded: &[*mut c_void],
    ) -> Option<NewMirror> {
        let raw = resource.as_raw();
        let key = raw as usize;
        if self.mirrors.contains_key(&key) || excluded.contains(&raw) {
            return None;
        }
        if !self.tracker.record(key, access) {
            return None;
        }
        self.tracker.remove(key);
        let Some(mirror_resource) = create_mirror_resource(device, resource) else {
            log::info!("not mirroring eye history resource {key:#x}");
            self.mirrors.insert(key, None);
            return None;
        };
        log::info!("mirroring eye history resource {key:#x}");
        self.live_mirror_count += 1;
        self.mirrors.insert(
            key,
            Some(Mirror {
                resource: mirror_resource.clone(),
                views: HashMap::new(),
            }),
        );
        Some(NewMirror {
            original: resource.clone(),
            mirror: mirror_resource,
        })
    }

    pub(crate) fn mirror_resource(&self, resource: *mut c_void) -> Option<*mut c_void> {
        let mirror = self.mirrors.get(&(resource as usize))?.as_ref()?;
        Some(mirror.resource.as_raw())
    }

    pub(crate) fn substitute(
        &mut self,
        device: &ID3D11Device,
        kind: ViewKind,
        views: &[*mut c_void],
    ) -> Option<Vec<*mut c_void>> {
        if !self.has_mirrors() {
            return None;
        }
        let mut substituted: Option<Vec<*mut c_void>> = None;
        for (i, &view) in views.iter().enumerate() {
            let Some(mirror_view) = self.mirror_view(device, kind, view) else {
                continue;
            };
            substituted.get_or_insert_with(|| views.to_vec())[i] = mirror_view;
        }
        substituted
    }

    pub(crate) fn mirror_view(
        &mut self,
        device: &ID3D11Device,
        kind: ViewKind,
        view: *mut c_void,
    ) -> Option<*mut c_void> {
        let original = unsafe { ID3D11View::from_raw_borrowed(&view) }?;
        let resource = unsafe { original.GetResource() }.ok()?;
        let mirror = self
            .mirrors
            .get_mut(&(resource.as_raw() as usize))?
            .as_mut()?;
        if let Some(existing) = mirror.views.get(&(view as usize)) {
            return Some(existing.as_raw());
        }
        let created = create_mirror_view(device, kind, original, &mirror.resource)?;
        let raw = created.as_raw();
        mirror.views.insert(view as usize, created);
        self.originals_by_mirror_view
            .insert(raw as usize, view as usize);
        Some(raw)
    }

    pub(crate) fn original_view(&self, mirror_view: *mut c_void) -> Option<*mut c_void> {
        self.originals_by_mirror_view
            .get(&(mirror_view as usize))
            .map(|&view| view as *mut c_void)
    }
}

fn is_too_small(texels: u64) -> bool {
    texels <= MAX_UNMIRRORED_TEXELS
}

fn create_mirror_resource(
    device: &ID3D11Device,
    original: &ID3D11Resource,
) -> Option<ID3D11Resource> {
    match unsafe { original.GetType() } {
        D3D11_RESOURCE_DIMENSION_TEXTURE2D => {
            let typed: ID3D11Texture2D = original.cast().ok()?;
            let mut desc = D3D11_TEXTURE2D_DESC::default();
            unsafe { typed.GetDesc(&mut desc) };
            if is_too_small(u64::from(desc.Width) * u64::from(desc.Height)) {
                return None;
            }
            let mut out = None;
            unsafe { device.CreateTexture2D(&desc, None, Some(&mut out)) }.ok()?;
            out.map(Into::into)
        }
        _ => None,
    }
}

fn create_mirror_view(
    device: &ID3D11Device,
    kind: ViewKind,
    original: &ID3D11View,
    resource: &ID3D11Resource,
) -> Option<ID3D11View> {
    match kind {
        ViewKind::ShaderResource => {
            let typed: ID3D11ShaderResourceView = original.cast().ok()?;
            let mut desc = D3D11_SHADER_RESOURCE_VIEW_DESC::default();
            unsafe { typed.GetDesc(&mut desc) };
            let mut out = None;
            unsafe { device.CreateShaderResourceView(resource, Some(&desc), Some(&mut out)) }
                .ok()?;
            out.map(Into::into)
        }
        ViewKind::UnorderedAccess => {
            let typed: ID3D11UnorderedAccessView = original.cast().ok()?;
            let mut desc = D3D11_UNORDERED_ACCESS_VIEW_DESC::default();
            unsafe { typed.GetDesc(&mut desc) };
            let mut out = None;
            unsafe { device.CreateUnorderedAccessView(resource, Some(&desc), Some(&mut out)) }
                .ok()?;
            out.map(Into::into)
        }
        ViewKind::RenderTarget => {
            let typed: ID3D11RenderTargetView = original.cast().ok()?;
            let mut desc = D3D11_RENDER_TARGET_VIEW_DESC::default();
            unsafe { typed.GetDesc(&mut desc) };
            let mut out = None;
            unsafe { device.CreateRenderTargetView(resource, Some(&desc), Some(&mut out)) }.ok()?;
            out.map(Into::into)
        }
        ViewKind::DepthStencil => {
            let typed: ID3D11DepthStencilView = original.cast().ok()?;
            let mut desc = D3D11_DEPTH_STENCIL_VIEW_DESC::default();
            unsafe { typed.GetDesc(&mut desc) };
            let mut out = None;
            unsafe { device.CreateDepthStencilView(resource, Some(&desc), Some(&mut out)) }.ok()?;
            out.map(Into::into)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const A: usize = 0x1000;
    const B: usize = 0x2000;

    #[test]
    fn ping_pong_pair_qualifies_within_two_frames() {
        let mut tracker = UsageTracker::default();
        let mut qualified = [false; 2];
        for frame in 0..3 {
            let (history, output) = if frame % 2 == 0 { (A, B) } else { (B, A) };
            for _eye in 0..2 {
                tracker.begin_pass();
                if tracker.record(history, Access::Read) {
                    qualified[usize::from(history == B)] = true;
                }
                tracker.record(output, Access::Write);
                tracker.record(output, Access::Read);
            }
            tracker.forget_unwritten();
        }
        assert_eq!(qualified, [true, true]);
    }

    #[test]
    fn transient_resource_never_qualifies() {
        let mut tracker = UsageTracker::default();
        for _pass in 0..4 {
            tracker.begin_pass();
            assert!(!tracker.record(A, Access::Write));
            assert!(!tracker.record(A, Access::Read));
            assert!(!tracker.record(A, Access::Read));
        }
    }

    #[test]
    fn resource_never_written_in_eye_pass_never_qualifies() {
        let mut tracker = UsageTracker::default();
        for _pass in 0..4 {
            tracker.begin_pass();
            assert!(!tracker.record(A, Access::Read));
            tracker.forget_unwritten();
        }
    }

    #[test]
    fn begin_pass_resets_written_this_pass() {
        let mut tracker = UsageTracker::default();
        tracker.begin_pass();
        tracker.record(A, Access::Write);
        assert!(!tracker.record(A, Access::Read));
        tracker.begin_pass();
        assert!(tracker.record(A, Access::Read));
    }

    #[test]
    fn tiny_textures_are_not_mirrored() {
        assert!(is_too_small(1));
        assert!(is_too_small(16));
        assert!(!is_too_small(64 * 64));
    }
}
