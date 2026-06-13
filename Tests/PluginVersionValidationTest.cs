using System.Text.Json;

namespace Tests;

[TestFixture]
public class PluginVersionValidationTest
{
    private const string PluginMasterPath = "pluginmaster.json";
    private const string ProjectFilePath = "FfxivVr.csproj";

    private JsonElement ReadRepoJson()
    {
        var pluginMasterJson = File.ReadAllText(PluginMasterPath);
        var pluginData = JsonSerializer.Deserialize<JsonElement[]>(pluginMasterJson);
        if (pluginData == null)
        {
            throw new Exception("Missing plugin");
        }
        return pluginData[0];
    }

    [Test]
    public void DalamudSdkVersionMatchesApiLevel()
    {
        var plugin = ReadRepoJson();

        var apiLevel = plugin.GetProperty("DalamudApiLevel").GetInt32();

        var projectFileContent = File.ReadAllText(ProjectFilePath);

        var sdkMatch = System.Text.RegularExpressions.Regex.Match(projectFileContent,
            @"Project Sdk=""Dalamud\.NET\.Sdk/(\d+(?:\.\d+)*)""");

        Assert.IsTrue(sdkMatch.Success, "Could not find Dalamud.NET.Sdk version in .csproj file");

        var sdkVersion = sdkMatch.Groups[1].Value;

        Assert.That(sdkVersion, Does.StartWith(apiLevel.ToString()));
    }

    [Test]
    public void AssemblyName()
    {
        Assert.That(typeof(FfxivVR.Plugin).Assembly.GetName().Name, Is.EqualTo("FfxivVR"));

        var plugin = ReadRepoJson();
        Assert.That(plugin.GetProperty("InternalName").GetString(), Is.EqualTo("FfxivVR"));
    }
}