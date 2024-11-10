
using Silk.NET.Maths;
using System;

namespace FfxivVR;

// Gets the origin view position of the VR camera, VR view offsets are applied afterwards
abstract class VRCameraType(Vector3D<float> gameCameraPosition, Vector3D<float> gameCameraLookAt)
{
    public readonly Vector3D<float> GameCameraPosition = gameCameraPosition;
    public readonly Vector3D<float> GameCameraLookAt = gameCameraLookAt;

    public readonly Vector3D<float> GameCameraForwardVector = gameCameraLookAt - gameCameraPosition;

    public abstract Vector3D<float> GetCameraPosition();

    // Most camera won't change the rotation so provide a default implementation
    public Quaternion<float> GetCameraRotation()
    {
        return MathFactory.YRotation(GetYRotation());
    }

    public float GetYRotation()
    {
        return -MathF.PI / 2 - MathF.Atan2(GameCameraForwardVector.Z, GameCameraForwardVector.X);
    }
}

class OrbitCamera : VRCameraType
{
    public OrbitCamera(Vector3D<float> gameCameraPosition, Vector3D<float> gameCameraLookAt) : base(gameCameraPosition, gameCameraLookAt)
    {
    }

    public override Vector3D<float> GetCameraPosition() { return GameCameraPosition; }
}

class FirstPersonCamera : OrbitCamera
{

    // This just works the same way as the orbit camera
    public FirstPersonCamera(Vector3D<float> gameCameraPosition, Vector3D<float> gameCameraLookAt) : base(gameCameraPosition, gameCameraLookAt)
    {
    }
}

class FollowingFirstPersonCamera : VRCameraType
{
    public FollowingFirstPersonCamera(Vector3D<float> gameCameraPosition, Vector3D<float> gameCameraLookAt, Vector3D<float> headPosition) : base(gameCameraPosition, gameCameraLookAt)
    {
        HeadPosition = headPosition;
    }

    public Vector3D<float> HeadPosition { get; }

    public override Vector3D<float> GetCameraPosition()
    {
        return HeadPosition;
    }
}

class LockedFloorCamera : VRCameraType
{
    public LockedFloorCamera(Vector3D<float> gameCameraPosition, Vector3D<float> gameCameraLookAt, float groundPosition, float height, float distance, float worldScale) : base(gameCameraPosition, gameCameraLookAt)
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

    public override Vector3D<float> GetCameraPosition()
    {
        var pos = GameCameraLookAt - Vector3D.Transform(-Vector3D<float>.UnitZ * Distance, MathFactory.YRotation(GetYRotation()));
        pos.Y = GroundPosition + Height / WorldScale;
        return pos;
    }
}
