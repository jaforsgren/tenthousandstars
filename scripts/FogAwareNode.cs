using Godot;

namespace Tts;

public abstract partial class FogAwareNode : Node2D
{
	private static readonly Color ScoutedModulate = new(0.5f, 0.55f, 0.65f, 0.45f);

	public FogState FogState { get; private set; } = FogState.Revealed;

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

		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", targetModulate, clearSeconds);
	}
}
