using Godot;

namespace Tts.Effects;

// Visual layers (ImpactLayer, CaptureLayer) and all animation data live in
// CombatEffectNode.tscn — edit shapes and timings there in the Godot editor.
public partial class CombatEffectNode : Node2D
{
	private AnimationPlayer _player = null!;

	public override void _Ready()
	{
		_player = GetNode<AnimationPlayer>("AnimationPlayer");
		_player.AnimationFinished += _ => QueueFree();
	}

	public void PlayImpact() => _player.Play("impact");
	public void PlayCapture() => _player.Play("capture");
}
