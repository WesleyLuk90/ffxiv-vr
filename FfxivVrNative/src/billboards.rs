use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_CONSTANT_BUFFER, D3D11_BUFFER_DESC, D3D11_CPU_ACCESS_WRITE, D3D11_USAGE_DYNAMIC,
    ID3D11Buffer, ID3D11Device, ID3D11GeometryShader,
};

use crate::math::{Mat4, Vec4};

const BYTECODE: &[u8] = include_bytes!("../../FfxivVr/shaders/BillboardShader.cso");
pub const MAX_ANCHORS: usize = 50;

#[repr(C)]
#[derive(Clone, Copy)]
pub struct BillboardConstants {
    pub projection: Mat4,
    pub inverse_projection: Mat4,
    pub screen_up: Vec4,
    pub world_up: Vec4,
    pub anchor_count: u32,
    _padding: [u32; 3],
    pub anchors: [Vec4; MAX_ANCHORS],
    pub targets: [Vec4; MAX_ANCHORS],
}

#[derive(Clone, Copy)]
pub struct NameplateAnchor {
    pub screen: Vec4,
    pub world: Option<Vec4>,
}

#[derive(Clone, Copy, PartialEq)]
pub struct BillboardInputs {
    pub projection: Mat4,
    pub clip_from_screen: Mat4,
    pub view_from_world: Mat4,
    pub screen_up: Vec4,
    pub world_up: Vec4,
}

pub struct Billboards {
    pub shader: ID3D11GeometryShader,
    pub constants: ID3D11Buffer,
    pub uploaded: Option<BillboardInputs>,
}

impl Billboards {
    pub fn new(device: &ID3D11Device) -> Option<Self> {
        let billboards = Self::create(device);
        if billboards.is_none() {
            log::error!("billboard shader unavailable, nameplates will not face the eye");
        }
        billboards
    }

    fn create(device: &ID3D11Device) -> Option<Self> {
        let mut shader = None;
        unsafe { device.CreateGeometryShader(BYTECODE, None, Some(&mut shader)) }.ok()?;
        let desc = D3D11_BUFFER_DESC {
            ByteWidth: std::mem::size_of::<BillboardConstants>() as u32,
            Usage: D3D11_USAGE_DYNAMIC,
            BindFlags: D3D11_BIND_CONSTANT_BUFFER.0 as u32,
            CPUAccessFlags: D3D11_CPU_ACCESS_WRITE.0 as u32,
            ..Default::default()
        };
        let mut constants = None;
        unsafe { device.CreateBuffer(&desc, None, Some(&mut constants)) }.ok()?;
        Some(Self {
            shader: shader?,
            constants: constants?,
            uploaded: None,
        })
    }
}

impl BillboardConstants {
    pub fn new(inputs: &BillboardInputs, nameplate_anchors: &[NameplateAnchor]) -> Option<Self> {
        let inverse_projection = inputs.projection.invert()?;
        let screen_to_view = inverse_projection.mul(&inputs.clip_from_screen);
        let mut anchors = [Vec4::default(); MAX_ANCHORS];
        let mut targets = [Vec4::default(); MAX_ANCHORS];
        let mut anchor_count = 0;
        for anchor in nameplate_anchors.iter().take(MAX_ANCHORS) {
            let Some(view) = to_point(screen_to_view.transform(anchor.screen)) else {
                continue;
            };
            anchors[anchor_count] = view;
            targets[anchor_count] = anchor
                .world
                .and_then(|world| to_point(inputs.view_from_world.transform(world)))
                .unwrap_or(view);
            anchor_count += 1;
        }
        Some(Self {
            projection: inputs.projection,
            inverse_projection,
            screen_up: inputs.screen_up,
            world_up: inputs.world_up,
            anchor_count: anchor_count as u32,
            _padding: [0; 3],
            anchors,
            targets,
        })
    }

    pub fn as_bytes(&self) -> &[u8] {
        unsafe {
            std::slice::from_raw_parts(
                self as *const Self as *const u8,
                std::mem::size_of::<Self>(),
            )
        }
    }
}

fn to_point(v: Vec4) -> Option<Vec4> {
    (v.w.abs() >= 1e-6).then(|| Vec4::new(v.x / v.w, v.y / v.w, v.z / v.w, 1.0))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn shader_anchor_count_matches() {
        let source = include_str!("../../FfxivVr/shaders/BillboardShader.hlsl");
        assert!(source.contains(&format!("#define MAX_ANCHORS {MAX_ANCHORS}\n")));
    }

    #[test]
    fn constants_match_shader_layout() {
        assert_eq!(
            std::mem::size_of::<BillboardConstants>(),
            176 + 32 * MAX_ANCHORS
        );
    }
}
