using Silk.NET.OpenXR;

namespace FfxivVR;

public class EyeRender(
    Eye eye,
    View view
)
{
    public Eye Eye { get; } = eye;
    public View View { get; } = view;
}