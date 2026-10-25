namespace FfxivVR;

public unsafe class LetterboxModifier(
    Configuration configuration)
{
    internal void UpdateLetterboxing(LetterboxingExtended* internalLetterbox)
    {
        if (configuration.DisableCutsceneLetterbox)
        {
            internalLetterbox->ShouldLetterBox &= ~LetterBoxingOption.EnableLetterboxing;
        }
    }
}