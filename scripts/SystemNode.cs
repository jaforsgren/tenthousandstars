using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

public partial class SystemNode : Node2D
{
	private const float SystemRadius = 50f;
	private const float FleetCircleRadius = 16f;
	private const float FleetCircleGap = 8f;
	private const float BaseProduction = 0.3f;
	private const float LabelWidth = 36f;
	private const float LabelHeight = 18f;

	private static readonly Color SystemFill = new(0.2f, 0.4f, 0.75f, 0.35f);
	private static readonly Color SystemOutline = new(0.5f, 0.7f, 1.0f, 0.9f);
	private static readonly Color PlanetFill = new(0.55f, 0.8f, 0.4f);
	private static readonly Color FleetFill = new(0.9f, 0.7f, 0.15f, 0.55f);
	private static readonly Color FleetOutline = new(1.0f, 0.9f, 0.4f);

	private IReadOnlyList<Planet> _planets = [];
	private float _ships;
	private Label _shipLabel = null!;

	public float ProductionRate => _planets.Sum(p => p.ProductionRate) + BaseProduction;

	public void Initialize(IReadOnlyList<Planet> planets, float initialShips = 0f)
	{
		_planets = planets;
		_ships = initialShips;
	}

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		var fleetCenterY = SystemRadius + FleetCircleGap + FleetCircleRadius;
		_shipLabel = new Label
		{
			Position = new Vector2(-LabelWidth / 2f, fleetCenterY - LabelHeight / 2f),
			Size = new Vector2(LabelWidth, LabelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "0"
		};
		_shipLabel.AddThemeColorOverride("font_color", Colors.White);
		_shipLabel.AddThemeFontSizeOverride("font_size", 11);
		AddChild(_shipLabel);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		_ships += ProductionRate * (float)delta;
		_shipLabel.Text = Mathf.FloorToInt(_ships).ToString();
	}

	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, SystemRadius, SystemFill);
		DrawArc(Vector2.Zero, SystemRadius, 0f, Mathf.Tau, 64, SystemOutline, 1.5f);

		foreach (var planet in _planets)
		{
			var pos = new Vector2(
				Mathf.Cos(planet.OrbitAngle) * planet.OrbitRadius,
				Mathf.Sin(planet.OrbitAngle) * planet.OrbitRadius
			);
			DrawCircle(pos, planet.Size, PlanetFill);
		}

		var fleetCenter = new Vector2(0f, SystemRadius + FleetCircleGap + FleetCircleRadius);
		DrawCircle(fleetCenter, FleetCircleRadius, FleetFill);
		DrawArc(fleetCenter, FleetCircleRadius, 0f, Mathf.Tau, 32, FleetOutline, 1.5f);
	}
}
