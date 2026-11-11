using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    public static string GameDirectory => Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();

    public static List<string> FindShaderModFiles()
    {
        if (CheckDisabled)
        {
            return [];
        }
        var gameDir = GameDirectory;
        return ShaderModFiles.Where(file => File.Exists(Path.Combine(gameDir, file))).ToList();
    }

    public static bool HasShaderMod()
    {
        return FindShaderModFiles().Count > 0;
    }
}