using Godot;
using Tts.Types;

namespace Tts.Nodes;

public abstract partial class FogAwareNode : Node2D
{
	private static readonly Color ScoutedModulate = new(0.5f, 0.55f, 0.65f, 0.45f);

	private Tween? _fogTween;

	public FogState FogState { get; private set; } = FogState.Hidden;

	public void SetFogState(FogState fogState, float clearSeconds)
	{
		var previousState = FogState;
		FogState = fogState;

		if (fogState == FogState.Hidden)
		{
			Visible = false;
			return;
		}

		var targetModulate = fogState == FogState.Scouted ? ScoutedModulate : Colors.White;
		Visible = true;

		if (previousState == FogState.Hidden)
			Modulate = targetModulate.WithAlpha(0f);

		_fogTween?.Kill();
		_fogTween = CreateTween();
		_fogTween.TweenProperty(this, "modulate", targetModulate, clearSeconds);
	}
}
