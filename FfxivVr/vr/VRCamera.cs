using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;
public class VRCamera(Configuration configuration, GameModifier gameModifier, GameState gameState, FreeCamera freeCamera)
{
    private float near = 0.1f;

    internal Matrix4x4 ComputeGameProjectionMatrix(View view)
    {
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

        // FFXIV uses reverse z matrixes, update the matrix to handle this
        proj.M33 = 0;
        proj.M43 = near;
        return proj.ToMatrix4x4();
    }
    internal Matrix4X4<float> ComputeGameViewMatrix(View view, VRCameraMode cameraMode, GameCamera gameCamera)
    {
        var cameraPosition = cameraMode.GetCameraPosition(gameCamera);

        var gameViewMatrix = cameraMode.GetRotationMatrix(gameCamera) * Matrix4X4.CreateTranslation(cameraPosition);
        var scaledPosition = view.Pose.Position.ToVector3D() / configuration.WorldScale;
        var vrViewMatrix = Matrix4X4.CreateFromQuaternion(view.Pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation(scaledPosition);

        var viewMatrix = vrViewMatrix * gameViewMatrix;
        Matrix4X4.Invert(viewMatrix, out Matrix4X4<float> invertedViewMatrix);
        return invertedViewMatrix;
    }

    internal Matrix4X4<float> ComputeVRViewProjectionMatrix(View view)
    {
        var rotation = Matrix4X4.CreateFromQuaternion(view.Pose.Orientation.ToQuaternion());
        var translation = Matrix4X4.CreateTranslation(view.Pose.Position.ToVector3D() / configuration.WorldScale);
        var toView = Matrix4X4.Multiply(rotation, translation);
        Matrix4X4.Invert(toView, out Matrix4X4<float> viewInverted);

        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

        // Also use reverse z matries for ours so we can use the depth buffer from the game
        proj.M33 = 0;
        proj.M43 = near;
        return Matrix4X4.Multiply(viewInverted, proj);
    }

    public unsafe VRCameraMode GetVRCameraType(float? localSpaceHeight, bool hasBodyData)
    {
        var characterBase = gameModifier.GetCharacterBase();
        var distance = gameState.GetGameCameraDistance();
        if (freeCamera.Enabled)
        {
            return freeCamera;
        }
        else if (gameState.IsOccupiedInCutSceneEvent() && !configuration.KeepCutsceneCameraHorizontal)
        {
            return new OrbitCamera();
        }
        else if (gameState.IsFirstPerson() && (hasBodyData || configuration.LockToHead))
        {
            return new BodyTrackingCamera();
        }
        else if (gameState.IsFirstPerson() && configuration.FollowCharacter)
        {
            return new FollowingFirstPersonCamera();
        }
        else if (gameState.IsFirstPerson())
        {
            return new FirstPersonCamera();
        }
        else if (localSpaceHeight is float height && characterBase != null && distance is float d)
        {
            return new LockedFloorCamera(
                groundPosition: characterBase->Position.Y,
                height: height + configuration.FloorHeightOffset,
                distance: d,
                worldScale: configuration.WorldScale);
        }
        else if (!configuration.KeepCameraHorizontal)
        {
            return new OrbitCamera();
        }
        else
        {
            return new LevelOrbitCamera();
        }
    }

    private Vector3D<float>? savedHeadPosition = null;

    public unsafe GameCamera? CreateGameCamera()
    {
        var camera = gameState.GetCurrentCamera();
        if (camera == null)
        {
            return null;
        }
        var position = camera->Position.ToVector3D();
        var lookAt = camera->LookAtVector.ToVector3D();

        var transform = gameModifier.GetCharacterPositionTransform();
        var head = gameModifier.GetHeadOffset();
        if (savedHeadPosition == null)
        {
            savedHeadPosition = head;
        }
        Vector3D<float>? globalHead = null;
        if (transform is { } t)
        {
            if (head is { } h)
            {
                globalHead = Vector3D.Transform(h, t);
            }
        }
        return new GameCamera(position, lookAt, globalHead);
    }

    internal void ResetSavedHeadPosition()
    {
        savedHeadPosition = null;
    }

    internal unsafe void UpdateCamera(Camera* camera, GameCamera? gameCamera, VRCameraMode cameraType, View view)
    {
        camera->RenderCamera->ProjectionMatrix = ComputeGameProjectionMatrix(view);
        camera->RenderCamera->ProjectionMatrix2 = camera->RenderCamera->ProjectionMatrix;

        if (gameCamera == null)
        {
            return;
        }
        camera->RenderCamera->ViewMatrix = ComputeGameViewMatrix(view, cameraType, gameCamera).ToMatrix4x4();
        camera->ViewMatrix = camera->RenderCamera->ViewMatrix;

        camera->RenderCamera->FoV = view.Fov.AngleRight - view.Fov.AngleLeft;
    }
}