using Silk.NET.Maths;
using System.Diagnostics;
using System.Numerics;

namespace FfxivVR.Tests;

public class InverseKinematicsTests
{
    [Test]
    public void Calculate2BoneTest1()
    {
        Test2BoneIK(
            new Vector3D<float>(0, 0, 0),
            new Vector3D<float>(1, 0, 0),
            new Vector3D<float>(2, 0, 0),
            new Vector3D<float>(1, 0, 1));

    }
    [Test]
    public void Calculate2BoneTest2()
    {
        Test2BoneIK(
            new Vector3D<float>(0, 0, 0),
            new Vector3D<float>(1, 0, 0),
            new Vector3D<float>(2, 0, 0),
            new Vector3D<float>(1, 0, 0));
    }
    [Test]
    public void Calculate2BoneTest3()
    {
        Test2BoneIK(
            new Vector3D<float>(0, 0, 0),
            new Vector3D<float>(1, 0, 0),
            new Vector3D<float>(2, 0, 0),
            new Vector3D<float>(1, 0, 1));
        Test2BoneIK(
            new Vector3D<float>(0, 1, 0),
            new Vector3D<float>(1, 1, 0),
            new Vector3D<float>(2, 1, 0),
            new Vector3D<float>(1, 1, 1));
        Test2BoneIK(
            new Vector3D<float>(1, 1, 1),
            new Vector3D<float>(2, 1, 1),
            new Vector3D<float>(3, 1, 1),
            new Vector3D<float>(2, 1, 2));
    }
    [Test]
    public void Calculate2BoneTesta()
    {
        Test2BoneIK(
            new Vector3D<float>(0, 0, 0),
            new Vector3D<float>(0.5f, 0, MathF.Sqrt(1 - 0.5f * 0.5f)), // 0.866
            new Vector3D<float>(1, 0, 0),
            new Vector3D<float>(2, 0, 0));
    }
    [Test]
    public void Calculate2BoneTest4()
    {
        Test2BoneIK(
            new Vector3D<float>(-0.16794066f, 1.3948892f, 0.029538013f),
            new Vector3D<float>(-0.36991543f, 1.1961719f, 0.075216964f),
            new Vector3D<float>(-0.52466637f, 1.0088096f, 0.16539592f),
            new Vector3D<float>(-0.062444247f, 1.2952111f, 0.3459903f));
    }

    [Test]
    public void Calculate2BoneTest5()
    {
        Test2BoneIK(
            new Vector3D<float>(-0.16770929f, 1.3952233f, 0.028042033f),
            new Vector3D<float>(-0.36947682f, 1.1964281f, 0.07429442f),
            new Vector3D<float>(-0.5241992f, 1.0093637f, 0.16513847f),
            new Vector3D<float>(-0.13013679f, 1.2479662f, 0.33252394f));
    }

    [Test]
    public void Calculate2BoneTest6()
    {
        Test2BoneIK(
            new Vector3D<float>(0.07797372f, 1.4141837f, -0.15328033f),
            new Vector3D<float>(0.2835403f, 1.2345111f, -0.24176377f),
            new Vector3D<float>(0.4794166f, 1.066514f, -0.26614535f),
            new Vector3D<float>(0.2669642f, 1.5886016f, -0.04036241f));
    }


    private void Test2BoneIK(Vector3D<float> a, Vector3D<float> b, Vector3D<float> c, Vector3D<float> target)
    {
        var (r1, r2) = new InverseKinematics().Calculate2Bone(
            a: a,
            b: b,
            c: c,
            target: target,
            bendDirection: new Vector3D<float>(0, 0, -1));

        var ba = b - a;
        var cb = c - b;

        var cbRotated = Vector3D.Transform(cb, r2);
        var baCbRotated = ba + cbRotated;

        // After applying r2, the length of AB + BC*r2 should be the same as target - a
        Assert.That(baCbRotated.Length, Is.EqualTo((target - a).Length).Within(Epsilon));
        // After rotating both by r1 the length is the same
        Assert.That(Vector3D.Transform(baCbRotated, r1).Length, Is.EqualTo((target - a).Length).Within(Epsilon));

        // Make sure the total length is longer than the distance to the target
        Assert.IsTrue((c - b).Length + (b - a).Length >= (target - a).Length);
        // Check the final rotation is at the target
        AssertClose(a + Vector3D.Transform(baCbRotated, r1), target);
        AssertClose(a + Vector3D.Transform(ba, r1) + Vector3D.Transform(cb, r1 * r2), target);
    }

    private const float Epsilon = 0.005f;

    [Test]
    public void Calculate2BoneTestStraight()
    {
        var (a, b) = new InverseKinematics().Calculate2Bone(
            a: new Vector3D<float>(0, 0, 0),
            b: new Vector3D<float>(1, 0, 0),
            c: new Vector3D<float>(2, 0, 0),
            target: new Vector3D<float>(3, 0, 0),
            bendDirection: new Vector3D<float>(0, 0, -1));

        AssertClose(Quaternion<float>.Identity, a, 0.04f);
        AssertClose(Quaternion<float>.Identity, b, 0.04f);
    }

    public void AssertClose(Quaternion<float> expected, Quaternion<float> actual, float? epsilon = null)
    {
        Assert.That(expected.X, Is.EqualTo(epsilon).Within(epsilon ?? Epsilon), $"Expected {expected} to be close to {actual}");
        Assert.That(expected.Y, Is.EqualTo(actual.Y).Within(epsilon ?? Epsilon), $"Expected {expected} to be close to {actual}");
        Assert.That(expected.Z, Is.EqualTo(actual.Z).Within(epsilon ?? Epsilon), $"Expected {expected} to be close to {actual}");
        Assert.That(expected.W, Is.EqualTo(actual.W).Within(epsilon ?? Epsilon), $"Expected {expected} to be close to {actual}");
    }

    public void AssertClose(Vector3D<float> expected, Vector3D<float> actual)
    {
        Assert.That(expected.X, Is.EqualTo(actual.X).Within(Epsilon), $"Expected {expected} to be close to {actual}");
        Assert.That(expected.Y, Is.EqualTo(actual.Y).Within(Epsilon), $"Expected {expected} to be close to {actual}");
        Assert.That(expected.Z, Is.EqualTo(actual.Z).Within(Epsilon), $"Expected {expected} to be close to {actual}");
    }
}