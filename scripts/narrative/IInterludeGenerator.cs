namespace Tts;

public interface IInterludeGenerator
{
    InterludeContent? TryGenerate(MissionContext ctx);
}
