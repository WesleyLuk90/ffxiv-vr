using Silk.NET.OpenXR;

namespace FfxivVR;

public class EyeRender(
    Eye eye,
    View view,
    float uiRotation
)
{
    public Eye Eye { get; } = eye;
    public View View { get; } = view;
    public float UIRotation { get; } = uiRotation;
}