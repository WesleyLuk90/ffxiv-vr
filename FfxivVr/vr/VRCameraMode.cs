using Silk.NET.Maths;
using System;

namespace FfxivVR;

// Need to be careful here as the game camera values change slightly between the left and right frame rendering
public class GameCamera(Vector3D<float> position, Vector3D<float> lookAt, Vector3D<float>? headPosition, Vector3D<float> fixedHeadPosition, float? yRotation)
{
    public readonly Vector3D<float> GameCameraForwardVector = lookAt - position;

    public Vector3D<float> Position { get; } = position;
    public Vector3D<float> LookAt { get; } = lookAt;
    public Vector3D<float>? HeadPosition { get; } = headPosition;
    public Vector3D<float> FixedHeadPosition { get; } = fixedHeadPosition;

    public float? YRotation = yRotation;

    public virtual float GetYRotation()
    {
        return YRotation ?? -MathF.PI / 2 - MathF.Atan2(GameCameraForwardVector.Z, GameCameraForwardVector.X);
    }

}

// Gets the origin view position of the VR camera, VR view offsets are applied afterwards
public abstract class VRCameraMode
{

    public abstract Vector3D<float> GetCameraPosition(GameCamera gameCamera);

    // Most camera won't change the rotation so provide a default implementation
    public virtual float GetYRotation(GameCamera gameCamera)
    {
        return gameCamera.GetYRotation();
    }

    public virtual Matrix4X4<float> GetRotationMatrix(GameCamera gameCamera)
    {
        return Matrix4X4.CreateRotationY(GetYRotation(gameCamera));
    }
    public virtual bool ShouldLockCameraVerticalRotation { get; } = false;
    public virtual bool UseHeadMovement { get; } = true;
}

class OrbitCamera() : VRCameraMode
{
    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera) { return gameCamera.Position; }

    public override Matrix4X4<float> GetRotationMatrix(GameCamera gameCamera)
    {
        var cameraFacing = gameCamera.Position - gameCamera.LookAt;
        var angle = MathF.Asin(cameraFacing.Y / cameraFacing.Length);
        return Matrix4X4.CreateRotationX(-angle) * Matrix4X4.CreateRotationY(GetYRotation(gameCamera));
    }
}

class LevelOrbitCamera() : VRCameraMode
{
    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera) { return gameCamera.Position; }
}


class FirstPersonCamera : LevelOrbitCamera
{
    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera) { return gameCamera.FixedHeadPosition; }
}

class FollowingFirstPersonCamera : VRCameraMode
{

    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera)
    {
        if (gameCamera.HeadPosition is Vector3D<float> headPosition)
        {
            return headPosition;
        }
        else
        {
            return gameCamera.FixedHeadPosition;
        }
    }
}
class BodyTrackingCamera : FollowingFirstPersonCamera
{
    public override bool UseHeadMovement { get; } = false;
}

class LockedFloorCamera : VRCameraMode
{
    public LockedFloorCamera(float groundPosition, float height, float distance, float worldScale)
    {
        GroundPosition = groundPosition;
        Height = height;
        Distance = distance;
        WorldScale = worldScale;
    }
    public float GroundPosition { get; }
    public float Height { get; }
    public float Distance { get; }
    public float WorldScale { get; }

    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera)
    {
        var pos = gameCamera.LookAt - Vector3D.Transform(-Vector3D<float>.UnitZ * Distance, MathFactory.YRotation(gameCamera.GetYRotation()));
        pos.Y = GroundPosition + Height / WorldScale;
        return pos;
    }

    public override bool ShouldLockCameraVerticalRotation { get; } = true;
}