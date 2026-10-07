pub mod camera_parameters;
pub mod clip_to_world_matrix;
pub mod decal_parameter;
pub mod light_param;
pub mod projection_matrix;
pub mod ps_view_projection_inverse_matrix;
pub mod sky_quad_param;
pub mod sun_modifier;
pub mod vs_view_projection_matrix;
pub mod world_view_proj_matrix;

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Pass {
    Geometry,
    Compositing,
}
