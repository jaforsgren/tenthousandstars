using Tts.Narrative;
namespace Tts;

// Holds state that must survive scene reloads between game rounds.
internal static class GameSession
{
	internal static NarrativeController? NarrativeController { get; set; }
	internal static GameMode? GameModeOverride { get; set; }
}
