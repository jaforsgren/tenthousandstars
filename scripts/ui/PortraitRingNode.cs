using Godot;

namespace Tts.Ui;

[Tool]
public partial class PortraitRingNode : Control
{
	[Export] public Color RingColor { get => _ringColor; set { _ringColor = value; QueueRedraw(); } }
	[Export(PropertyHint.Range, "0.5,8.0")] public float RingWidth { get => _ringWidth; set { _ringWidth = value; QueueRedraw(); } }

	private Color _ringColor = new(0.85f, 0.85f, 0.85f, 0.5f);
	private float _ringWidth = 1.0f;

	public override void _Draw()
	{
		var radius = Mathf.Min(Size.X, Size.Y) * 0.5f;
		var center = Size * 0.5f;
		DrawArc(center, radius, 0, Mathf.Tau, 64, _ringColor, _ringWidth, antialiased: true);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}
}
