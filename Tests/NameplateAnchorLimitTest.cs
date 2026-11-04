namespace FfxivVR.Tests;

using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

class NameplateAnchorLimitTest
{
    [Test]
    public void NamePlateNumberArrayHasFiftyObjects()
    {
        // If this changes, update MAX_ANCHORS in FfxivVrNative/src/billboards.rs and FfxivVr/shaders/BillboardShader.hlsl.
        var numberArray = new NamePlateNumberArray();
        Assert.That(numberArray.ObjectData.Length, Is.EqualTo(50));
    }
}