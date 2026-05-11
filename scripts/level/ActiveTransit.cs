using Godot;

namespace Tts.Level;

internal sealed class ActiveTransit
{
	public required int FromIndex;
	public required int ToIndex;
	public required SystemOwner Owner;
	public required float Fleet;
	public required double LaunchTimeSec;
	public required float TotalDurationSec;
	public required TransitFleetNode Node;
	public required Vector2 ToWorldPos;
}
