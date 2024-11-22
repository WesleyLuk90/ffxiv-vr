using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Silk.NET.Maths;

namespace FfxivVR;

public unsafe class PositionSmoother
{
    private Vector3D<float>? lastPosition = null;
    private Vector3D<float> velocity = Vector3D<float>.Zero;

    private Character* lastCharacter = null;

    // The game reported position is very jittery on mounts so we motion smooth it
    public Vector3D<float> GetSmoothedPosition(Character* character)
    {
        if (character != lastCharacter)
        {
            Reset();
            lastCharacter = character;
        }
        var realPosition = character->Position.ToVector3D();
        Vector3D<float> smoothedPosition = realPosition;
        if (lastPosition is Vector3D<float> last && (last - realPosition).Length < 10)
        {
            var factor = 0.01f;
            // Use the smoothed velocity to predict our position
            smoothedPosition = last + velocity;
            // Blend the new position to update the velocity
            velocity = factor * (realPosition - last) + (1 - factor) * velocity;
        }
        lastPosition = realPosition;
        return smoothedPosition;
    }

    public void Reset()
    {
        lastPosition = null;
        velocity = Vector3D<float>.Zero;
    }

}