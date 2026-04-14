namespace Tts;

public interface IInterludeGenerator
{
    StoryText? TryGenerate(MissionContext ctx);
}
