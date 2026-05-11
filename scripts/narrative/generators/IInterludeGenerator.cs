namespace Tts.Narrative;

public interface IInterludeGenerator
{
    StoryText? TryGenerate(MissionContext ctx);
}
