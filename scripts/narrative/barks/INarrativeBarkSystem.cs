namespace Tts;

public interface INarrativeBarkSystem
{
    string? TryGetBark(BarkTrigger trigger, StoryState state);
}
