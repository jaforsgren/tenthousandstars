using Godot;

namespace Tts;

public partial class ProductionArcNode : Node2D
{
	private AnimationPlayer _animationPlayer = null!;
	private float _radius;
	private float _progress;

	private static readonly Color TrackColor = new(1f, 1f, 1f, 0.1f);
	private static readonly Color FillColor = new(1f, 1f, 1f, 0.65f);
	private const float ArcGap = 8f;
	private const float ArcWidth = 2.5f;
	private const int ArcSegments = 64;
	private const float StartAngle = -Mathf.Pi / 2f;  // 12 o'clock, clockwise

	public override void _Ready()
	{
		_animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
	}

	public void Initialize(float systemRadius)
	{
		_radius = systemRadius + ArcGap;
	}

	public void SetProgress(float progress)
	{
		_progress = Mathf.Clamp(progress, 0f, 1f);
		QueueRedraw();
	}

	public void PlayProduced()
	{
		_animationPlayer.Play("produced");
	}

	public override void _Draw()
	{
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, ArcSegments, TrackColor, ArcWidth);

		if (_progress > 0f)
			DrawArc(Vector2.Zero, _radius, StartAngle, StartAngle + _progress * Mathf.Tau, ArcSegments, FillColor, ArcWidth);
	}
}
