using Godot;

namespace Tts;

public partial class NeutralFleetNode : FleetNodeBase
{
	private static readonly Color NeutralFill = new(0.5f, 0.5f, 0.5f, 0.9f);

	public void Initialize(float systemRadius, float gap)
	{
		BaseInitialize(systemRadius, gap, NeutralFill);
	}
}
