using Godot;

namespace Tts.Nodes;

public partial class RerouteArrowNode : Node2D
{
	private const float ArrowGap = 5f;

	public void Initialize(Vector2 sourceWorldPos, Vector2 targetWorldPos, float systemRadius)
	{
		var direction = (targetWorldPos - sourceWorldPos).Normalized();
		Position = direction * (systemRadius + ArrowGap);
		Rotation = direction.Angle();
	}
}
