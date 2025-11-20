using System.IO;

namespace FfxivVR;

public static class ModDetection
{
    private static bool CheckDisabled = false;

    public static void DisableCheck()
    {
        CheckDisabled = true;
    }

    private static readonly string[] ShaderModFiles = new[]
    {
        "d3d9.dll",
        "dxgi.dll",
        "opengl32.dll"
    };
    public static bool HasShaderMod()
    {
        // If shader mod check is disabled in config, return false immediately
        if (CheckDisabled == true)
        {
            return false;
        }

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