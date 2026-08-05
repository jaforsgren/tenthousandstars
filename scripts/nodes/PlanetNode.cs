using Godot;

namespace Tts.Nodes;

public partial class PlanetNode : Node2D
{
	private float _size;
	private float _orbitRadius;
	private float _orbitAngle;
	private float _orbitSpeed;
	private ShaderMaterial _material = null!;
	private ColorRect _visualRect = null!;

	private const float MinTapRadius = 10f;
	private const string VisualScenePath = ScenePaths.PlanetNode;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene == null) return;
		var visual = scene.Instantiate();
		AddChild(visual);
		_visualRect = visual.GetNode<ColorRect>("PlanetVisual");
		_material = (ShaderMaterial)_visualRect.Material.Duplicate();
		_visualRect.Material = _material;
	}

	public void Initialize(Planet planet, Color innerColor, Color outerColor, float orbitSpeed)
	{
		_size = planet.Size;
		_orbitRadius = planet.OrbitRadius;
		_orbitAngle = planet.OrbitAngle;
		_orbitSpeed = orbitSpeed;

		Position = new Vector2(
			Mathf.Cos(_orbitAngle) * _orbitRadius,
			Mathf.Sin(_orbitAngle) * _orbitRadius
		);

		_visualRect.Size = new Vector2(_size * 2f, _size * 2f);
		_visualRect.Position = new Vector2(-_size, -_size);
		_material.SetShaderParameter("color_inner", innerColor);
		_material.SetShaderParameter("color_outer", outerColor);
		_material.SetShaderParameter("gradient_direction", (-Position).Normalized());
	}

	public bool ContainsPoint(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= Mathf.Max(_size, MinTapRadius);

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;
		_orbitAngle += _orbitSpeed * (float)delta;
		Position = new Vector2(
			Mathf.Cos(_orbitAngle) * _orbitRadius,
			Mathf.Sin(_orbitAngle) * _orbitRadius
		);
		_material.SetShaderParameter("gradient_direction", (-Position).Normalized());
	}
}
