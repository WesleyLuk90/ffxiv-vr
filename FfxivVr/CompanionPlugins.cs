using System.IO.MemoryMappedFiles;

namespace FfxivVR;
public class CompanionPlugins
{
    private MemoryMappedFile file;
    private MemoryMappedViewAccessor accessor;

    private int openOffset = 0;
    private int activeOffset = 16;

    // https://github.com/ProjectMimer/ConvenientGraphics/blob/dd4ebd3900caba5983dc6dc9c9580a5c8f6e1808/ConvenientGraphics/Structures/SharedMemoryManager.cs#L110
    public CompanionPlugins()
    {
        file = MemoryMappedFile.CreateOrOpen("projectMimerSharedMemory_8749602817645945", 1000);
        accessor = file.CreateViewAccessor();
    }
    public void OnLoad()
    {
        SetBit(openOffset, true);
    }
    public void OnActivate()
    {
        SetBit(activeOffset, true);
    }
    public void OnDeactivate()
    {
        SetBit(activeOffset, false);
    }
    public void OnUnload()
    {
        SetBit(openOffset, false);
    }

    private void SetBit(int offset, bool on)
    {
        var vrPluginBit = 1 << 1;
        var value = accessor.ReadUInt16(openOffset);
        if (on)
        {
            value |= (ushort)vrPluginBit;
        }
        else
        {
            value &= (ushort)~vrPluginBit;
        }
        accessor.Write(offset, value);
    }
}
