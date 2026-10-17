use core::ffi::c_void;

use windows::Win32::Graphics::Direct3D11::{
    D3D11_COMPARISON_GREATER_EQUAL, D3D11_DEPTH_STENCIL_DESC, D3D11_DEPTH_WRITE_MASK_ZERO,
    ID3D11DepthStencilState, ID3D11DepthStencilView, ID3D11Device, ID3D11RenderTargetView,
};

pub(crate) struct EyeSceneTarget {
    pub(crate) color: ID3D11RenderTargetView,
    pub(crate) depth: ID3D11DepthStencilView,
}

pub(crate) struct DepthTest {
    pub(crate) view: ID3D11DepthStencilView,
    pub(crate) state: ID3D11DepthStencilState,
}

pub(crate) struct TargetCursorDraw {
    pub(crate) texture: *mut c_void,
    pub(crate) depth: f32,
    pub(crate) eye_target: ID3D11RenderTargetView,
    pub(crate) depth_test: Option<DepthTest>,
}

pub(crate) struct TargetCursor {
    texture: *mut c_void,
    depth: Option<f32>,
    depth_tested: bool,
    depth_state: Option<ID3D11DepthStencilState>,
    eye_targets: Option<[EyeSceneTarget; 2]>,
}

impl TargetCursor {
    pub(crate) fn new(device: &ID3D11Device) -> Self {
        Self {
            texture: std::ptr::null_mut(),
            depth: None,
            depth_tested: false,
            depth_state: create_depth_state(device),
            eye_targets: None,
        }
    }

    pub(crate) fn set_texture(&mut self, texture: *mut c_void) {
        self.texture = texture;
    }

    pub(crate) fn set_depth(&mut self, depth: f32, depth_tested: bool) {
        if depth.is_finite() {
            self.depth = Some(depth);
        }
        self.depth_tested = depth_tested;
    }

    pub(crate) fn set_eye_targets(&mut self, eye_targets: Option<[EyeSceneTarget; 2]>) {
        self.eye_targets = eye_targets;
    }

    pub(crate) fn draw_for_eye(&self, eye: usize) -> Option<TargetCursorDraw> {
        if self.texture.is_null() {
            return None;
        }
        let eye_target = self.eye_targets.as_ref()?.get(eye)?;
        let depth_test = self
            .depth_state
            .as_ref()
            .filter(|_| self.depth_tested)
            .map(|state| DepthTest {
                view: eye_target.depth.clone(),
                state: state.clone(),
            });
        Some(TargetCursorDraw {
            texture: self.texture,
            depth: self.depth?,
            eye_target: eye_target.color.clone(),
            depth_test,
        })
    }
}

fn create_depth_state(device: &ID3D11Device) -> Option<ID3D11DepthStencilState> {
    let desc = D3D11_DEPTH_STENCIL_DESC {
        DepthEnable: true.into(),
        DepthWriteMask: D3D11_DEPTH_WRITE_MASK_ZERO,
        DepthFunc: D3D11_COMPARISON_GREATER_EQUAL,
        StencilEnable: false.into(),
        ..Default::default()
    };
    let mut state = None;
    unsafe { device.CreateDepthStencilState(&desc, Some(&mut state)) }.ok()?;
    state
}
