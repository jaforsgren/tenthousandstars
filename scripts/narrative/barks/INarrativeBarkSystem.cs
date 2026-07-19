namespace Tts.Narrative;

public interface INarrativeBarkSystem
{
    string? TryGetBark(BarkTrigger trigger);
}
