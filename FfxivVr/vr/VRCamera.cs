using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class VRCamera(
Configuration configuration,
GameModifier gameModifier,
GameState gameState,
FirstPersonManager firstPersonManager
)
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
    internal View ComputeCenterView(View left, View right)
    {
        var leftPosition = left.Pose.Position.ToVector3D();
        var rightPosition = right.Pose.Position.ToVector3D();
        var orientation = Quaternion<float>.Slerp(left.Pose.Orientation.ToQuaternion(), right.Pose.Orientation.ToQuaternion(), 0.5f);
        var fov = new Fovf(
            angleLeft: MathF.Min(left.Fov.AngleLeft, right.Fov.AngleLeft),
            angleRight: MathF.Max(left.Fov.AngleRight, right.Fov.AngleRight),
            angleUp: MathF.Max(left.Fov.AngleUp, right.Fov.AngleUp),
            angleDown: MathF.Min(left.Fov.AngleDown, right.Fov.AngleDown)
        );
        var halfSeparation = Vector3D.Distance(leftPosition, rightPosition) / 2;
        var pullBack = MathF.Max(halfSeparation / MathF.Tan(-fov.AngleLeft), halfSeparation / MathF.Tan(fov.AngleRight));
        var position = (leftPosition + rightPosition) / 2 + Vector3D.Transform(new Vector3D<float>(0, 0, pullBack), orientation);
        return new View
        {
            Type = StructureType.View,
            Pose = new Posef(orientation.ToQuaternionf(), position.ToVector3f()),
            Fov = fov,
        };
    }

    internal static Fovf WidenFovToAspect(Fovf fov, float aspect)
    {
        var left = MathF.Tan(fov.AngleLeft);
        var right = MathF.Tan(fov.AngleRight);
        var down = MathF.Tan(fov.AngleDown);
        var up = MathF.Tan(fov.AngleUp);
        var width = right - left;
        var height = up - down;
        if (width < height * aspect)
        {
            var grow = (height * aspect - width) / 2;
            left -= grow;
            right += grow;
        }
        else
        {
            var grow = (width / aspect - height) / 2;
            down -= grow;
            up += grow;
        }
        return new Fovf(
            angleLeft: MathF.Atan(left),
            angleRight: MathF.Atan(right),
            angleUp: MathF.Atan(up),
            angleDown: MathF.Atan(down)
        );
    }

    internal Matrix4x4 ComputeEyeDeltaView(View centerView, View eyeView)
    {
        var centerToWorld = ComputeEyeToWorld(centerView);
        Matrix4X4.Invert(ComputeEyeToWorld(eyeView), out var worldToEye);
        var deltaView = Matrix4X4.Multiply(centerToWorld, worldToEye);
        return Matrix4x4.Transpose(deltaView.ToMatrix4x4());
    }

    private Matrix4X4<float> ComputeEyeToWorld(View view)
    {
        var position = view.Pose.Position.ToVector3D() / configuration.WorldScale;
        return Matrix4X4.CreateFromQuaternion(view.Pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation(position);
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

    internal Vector3D<float> ComputeHeadWorldPosition(View left, View right, VRCameraMode cameraMode, GameCamera gameCamera)
    {
        var headPosition = cameraMode.UseHeadMovement
            ? (left.Pose.Position.ToVector3D() + right.Pose.Position.ToVector3D()) / 2 / configuration.WorldScale
            : Vector3D<float>.Zero;
        var gameViewMatrix = cameraMode.GetRotationMatrix(gameCamera) * Matrix4X4.CreateTranslation(cameraMode.GetCameraPosition(gameCamera));
        return Vector3D.Transform(headPosition, gameViewMatrix);
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
        var characterBase = gameState.GetCharacterBase();
        var distance = gameState.GetGameCameraDistance();
        if (gameState.IsOccupiedInCutSceneEvent())
        {
            if (configuration.KeepCutsceneCameraHorizontal)
            {
                return new LevelOrbitCamera();
            }
            else
            {
                return new OrbitCamera();
            }
        }
        else if (firstPersonManager.IsFirstPerson && (hasBodyData || configuration.LockToHead))
        {
            return new BodyTrackingCamera(configuration.FirstPersonHeightOffset);
        }
        else if (firstPersonManager.IsFirstPerson && configuration.FollowCharacter)
        {
            return new FollowingFirstPersonCamera(configuration.FirstPersonHeightOffset);
        }
        else if (firstPersonManager.IsFirstPerson)
        {
            return new FirstPersonCamera(configuration.FirstPersonHeightOffset);
        }
        else if (localSpaceHeight is float height && characterBase != null && distance is float d)
        {
            return new LockedFloorCamera(
                groundPosition: characterBase->Position.Y,
                height: height + configuration.FloorHeightOffset,
                distance: d,
                worldScale: configuration.WorldScale,
                sideOffset: configuration.ThirdPersonCameraSideOffset,
                forwardOffset: configuration.ThirdPersonCameraForwardOffset);
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
        Vector3D<float>? globalHead = null;
        if (transform is { } t)
        {
            if (head is { } h)
            {
                globalHead = Vector3D.Transform(h, t);
            }
        }
        return new GameCamera(position, lookAt, globalHead, gameState.GetFixedHeadPosition(), firstPersonManager.GetOffset());
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