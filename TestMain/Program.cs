// See https://aka.ms/new-console-template for more information
using FfxivVR;

Console.WriteLine("Hello, World!");

var main = new FfxivVR.VRSession(new DirectoryInfo(Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(VRSession)).Location
)));

main.Initialize();
