using Godot;

namespace Tts;

public partial class NeutralFleetNode : FleetNodeBase
{
	public void Initialize(float systemRadius, float gap, Color fill, Color outline)
	{
		BaseInitialize(systemRadius, gap, fill, outline);
	}
}
