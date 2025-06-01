using System.IO;

namespace FfxivVR;

public static class ModDetection
{

    private static readonly string[] ShaderModFiles = new[]
    {
        "d3d9.dll",
        "dxgi.dll",
        "opengl32.dll"
    };
    public static bool HasShaderMod()
    {
        var gameDir = Directory.GetCurrentDirectory();
        foreach (var file in ShaderModFiles)
        {
            var filePath = Path.Combine(gameDir, file);
            if (File.Exists(filePath))
            {
                return true;
            }
        }
        return false;
    }
}