using Silk.NET.Maths;

namespace FfxivVR;
public class FreeCamera
{
    public bool Enabled = false;
    private Vector3D<float>? SavedPosition = null;
    private float? SavedRotation = null;

    public void Update(ref Vector3D<float> position, ref float rotation)
    {
        if (Enabled && SavedPosition is Vector3D<float> savedPosition && SavedRotation is float savedRotation)
        {
            position = savedPosition;
            rotation = savedRotation;
        }
        else
        {
            SavedPosition = position;
            SavedRotation = rotation;
        }
    }
    public void UpdatePosition(Vector2D<float> walkDelta, float heightDelta, float rotationDelta)
    {
        if (Enabled && SavedPosition is Vector3D<float> position && SavedRotation is float rotation)
        {
            position.Y += heightDelta;
            var walk = new Vector3D<float>(walkDelta.X, 0, -walkDelta.Y);
            var newRotation = rotation + rotationDelta;
            var rotationMatrix = Matrix4X4.CreateRotationY(newRotation);
            position += Vector3D.Transform(walk, rotationMatrix);

            SavedPosition = position;
            SavedRotation = newRotation;
        }
    }
}
