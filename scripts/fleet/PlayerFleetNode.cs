using Godot;

namespace Tts;

public partial class PlayerFleetNode : FleetNodeBase
{
	private static readonly Color PlayerFill = new(0.9f, 0.28f, 0.1f, 0.9f);

	public void Initialize(float systemRadius, float gap)
	{
		BaseInitialize(systemRadius, gap, PlayerFill);
	}
}
