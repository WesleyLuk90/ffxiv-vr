
using Lumina.Data.Parsing.Layer;
using Silk.NET.Maths;
using System;

namespace FfxivVR;

// Need to be careful here as the game camera values change slightly between the left and right frame rendering
public class GameCamera(Vector3D<float> position, Vector3D<float> lookAt)
{
    public readonly Vector3D<float> GameCameraForwardVector = lookAt - position;

    public Vector3D<float> Position { get; } = position;
    public Vector3D<float> LookAt { get; } = lookAt;

    public virtual float GetYRotation()
    {
        return -MathF.PI / 2 - MathF.Atan2(GameCameraForwardVector.Z, GameCameraForwardVector.X);
    }

}

// Gets the origin view position of the VR camera, VR view offsets are applied afterwards
public abstract class VRCameraType
{

    public abstract Vector3D<float> GetCameraPosition(GameCamera gameCamera);

    // Most camera won't change the rotation so provide a default implementation
    public virtual float GetYRotation(GameCamera gameCamera)
    {
        return gameCamera.GetYRotation();
    }
    public virtual bool ShouldLockCameraVerticalRotation()
    {
        return false;
    }
}

class OrbitCamera() : VRCameraType
{
    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera) { return gameCamera.Position; }
}


// This just works the same way as the orbit camera
class FirstPersonCamera : OrbitCamera
{

}

class FollowingFirstPersonCamera : VRCameraType
{
    public FollowingFirstPersonCamera(Vector3D<float> headPosition)
    {
        HeadPosition = headPosition;
    }

    public Vector3D<float> HeadPosition { get; }

    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera)
    {
        return HeadPosition;
    }
}

class LockedFloorCamera : VRCameraType
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

    override public bool ShouldLockCameraVerticalRotation()
    {
        return true;
    }
}

class FreeCamera2 : VRCameraType
{
    public Vector3D<float> Position = Vector3D<float>.Zero;
    public float YRotation = 0;

    public override Vector3D<float> GetCameraPosition(GameCamera gameCamera)
    {
        return Position;
    }

    public override float GetYRotation(GameCamera gameCamera)
    {
        return YRotation;
    }
}