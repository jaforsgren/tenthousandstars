using Tts.Narrative;
namespace Tts;

// Holds state that must survive scene reloads between game rounds.
internal static class GameSession
{
	internal static CampaignController? Campaign { get; set; }
	internal static GameMode? GameModeOverride { get; set; }
}
