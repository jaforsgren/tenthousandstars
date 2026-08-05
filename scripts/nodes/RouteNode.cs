using Godot;
using Tts.Config;
using Tts.Utils;

namespace Tts.Nodes;

public partial class RouteNode : FogAwareNode
{
	private static readonly Color RouteColor          = new(0.45f, 0.5f, 0.65f, 0.5f);
	private static readonly Color PathHighlightColor  = new(1f, 0.85f, 0.2f, 0.85f);

	private Vector2 _from;
	private Vector2 _to;
	private float _routeWidth;
	private Color? _ownerColor;
	private bool _pathHighlighted;

	// Positions are in Level (parent) local space; RouteNode sits at (0,0) so local == parent local.
	public void Initialize(Vector2 from, Vector2 to, float routeWidth)
	{
		_from = from;
		_to = to;
		_routeWidth = routeWidth;
	}

	public void SetOwnerColor(Color? color)
	{
		_ownerColor = color;
		QueueRedraw();
	}

	public void SetPathHighlight(bool highlighted)
	{
		if (_pathHighlighted == highlighted) return;
		_pathHighlighted = highlighted;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_pathHighlighted)
		{
			DrawLine(_from, _to, PathHighlightColor, _routeWidth * 2f);
			return;
		}
		var color = _ownerColor.HasValue ? _ownerColor.Value.WithAlpha(0.5f) : RouteColor;
		DrawLine(_from, _to, color, _routeWidth);
	}
}
