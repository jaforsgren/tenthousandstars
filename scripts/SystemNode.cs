using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

public partial class SystemNode : Node2D
{
	private IReadOnlyList<Planet> _planets = [];
	private float _ships;
	private SystemOwner _owner;
	private Label _shipLabel = null!;

	private float _systemRadius;
	private float _fleetCircleRadius;
	private float _fleetCircleGap;
	private float _baseProduction;
	private float _labelWidth;
	private float _labelHeight;
	private Color _systemFill;
	private Color _systemOutline;
	private float _systemOutlineWidth;
	private Color _planetFill;
	private Color _planetOutline;
	private float _planetOutlineWidth;
	private Color _fleetFill;
	private Color _fleetOutline;
	private float _fleetOutlineWidth;
	private Color _neutralFleetFill;
	private Color _neutralFleetOutline;

	private const float SelectedOutlineWidthMultiplier = 4f;

	private bool _selected;

	public float ProductionRate => _planets.Sum(p => p.ProductionRate) + _baseProduction;
	public float Ships => _ships;
	public SystemOwner Owner => _owner;
	public bool HasFleet => _ships > 0;
	public bool IsPlayerOwned => _owner == SystemOwner.Player;

	public bool ContainsFleetAt(Vector2 worldPos)
	{
		var center = GlobalPosition + new Vector2(0f, _systemRadius + _fleetCircleGap + _fleetCircleRadius);
		return worldPos.DistanceTo(center) <= _fleetCircleRadius;
	}

	public bool ContainsSystemAt(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= _systemRadius;

	private const float MinPlanetTapRadius = 10f;

	public int? PlanetIndexAt(Vector2 worldPos)
	{
		for (var i = 0; i < _planets.Count; i++)
		{
			var planet = _planets[i];
			var center = GlobalPosition + new Vector2(
				Mathf.Cos(planet.OrbitAngle) * planet.OrbitRadius,
				Mathf.Sin(planet.OrbitAngle) * planet.OrbitRadius
			);
			if (worldPos.DistanceTo(center) <= Mathf.Max(planet.Size, MinPlanetTapRadius))
				return i;
		}
		return null;
	}

	public float TakeFleet()
	{
		var taken = _ships;
		_ships = 0f;
		_selected = false;
		UpdateLabel();
		QueueRedraw();
		return taken;
	}

	// Friendly reinforcement — same owner sends ships here.
	public void AddFleet(float ships)
	{
		_ships += ships;
		UpdateLabel();
		QueueRedraw();
	}

	// Attacker won combat — this system is captured with the attacker's remaining ships.
	public void Capture(float ships, SystemOwner newOwner)
	{
		_owner = newOwner;
		_ships = ships;
		UpdateLabel();
		QueueRedraw();
	}

	// Defender survived — update fleet to post-combat remainder.
	public void SustainDefense(float remainingShips)
	{
		_ships = Mathf.Max(0f, remainingShips);
		UpdateLabel();
		QueueRedraw();
	}

	public void SetSelected(bool selected)
	{
		_selected = selected;
		QueueRedraw();
	}

	public void Initialize(IReadOnlyList<Planet> planets, SystemOwner owner, float initialShips = 0f)
	{
		_planets = planets;
		_owner = owner;
		_ships = initialShips;
		if (_shipLabel != null)
			UpdateLabel();
	}

	private void UpdateLabel()
	{
		_shipLabel.Text = Mathf.FloorToInt(_ships).ToString();
		_shipLabel.Visible = _ships > 0;
	}

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_systemRadius = cfg.SystemRadius;
		_fleetCircleRadius = cfg.FleetCircleRadius;
		_fleetCircleGap = cfg.FleetCircleGap;
		_baseProduction = cfg.BaseProduction;
		_labelWidth = cfg.LabelWidth;
		_labelHeight = cfg.LabelHeight;
		_systemFill = cfg.SystemFill.ToColor();
		_systemOutline = cfg.SystemOutline.ToColor();
		_systemOutlineWidth = cfg.SystemOutlineWidth;
		_planetFill = cfg.PlanetFill.ToColor();
		_planetOutline = cfg.PlanetOutline.ToColor();
		_planetOutlineWidth = cfg.PlanetOutlineWidth;
		_fleetFill = cfg.FleetFill.ToColor();
		_fleetOutline = cfg.FleetOutline.ToColor();
		_fleetOutlineWidth = cfg.FleetOutlineWidth;
		_neutralFleetFill = cfg.NeutralFleetFill.ToColor();
		_neutralFleetOutline = cfg.NeutralFleetOutline.ToColor();

		if (Engine.IsEditorHint())
			return;

		var fleetCenterY = _systemRadius + _fleetCircleGap + _fleetCircleRadius;
		_shipLabel = new Label
		{
			Position = new Vector2(-_labelWidth / 2f, fleetCenterY - _labelHeight / 2f),
			Size = new Vector2(_labelWidth, _labelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "0",
			Visible = false
		};
		_shipLabel.AddThemeColorOverride("font_color", Colors.White);
		_shipLabel.AddThemeFontSizeOverride("font_size", 11);
		AddChild(_shipLabel);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _owner == SystemOwner.None)
			return;

		_ships += ProductionRate * (float)delta;
		UpdateLabel();
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawCircle(Vector2.Zero, _systemRadius, _systemFill);
		DrawArc(Vector2.Zero, _systemRadius, 0f, Mathf.Tau, 64, _systemOutline, _systemOutlineWidth);

		foreach (var planet in _planets)
		{
			var pos = new Vector2(
				Mathf.Cos(planet.OrbitAngle) * planet.OrbitRadius,
				Mathf.Sin(planet.OrbitAngle) * planet.OrbitRadius
			);
			DrawCircle(pos, planet.Size, _planetFill);
			DrawArc(pos, planet.Size, 0f, Mathf.Tau, 16, _planetOutline, _planetOutlineWidth);
		}

		if (_ships > 0)
		{
			var isPlayerFleet = _owner == SystemOwner.Player;
			var fill = isPlayerFleet ? _fleetFill : _neutralFleetFill;
			var outline = isPlayerFleet ? _fleetOutline : _neutralFleetOutline;
			var fleetCenter = new Vector2(0f, _systemRadius + _fleetCircleGap + _fleetCircleRadius);
			DrawCircle(fleetCenter, _fleetCircleRadius, fill);
			var outlineWidth = _selected ? _fleetOutlineWidth * SelectedOutlineWidthMultiplier : _fleetOutlineWidth;
			var outlineColor = _selected ? Colors.White : outline;
			DrawArc(fleetCenter, _fleetCircleRadius, 0f, Mathf.Tau, 32, outlineColor, outlineWidth);
		}
	}
}
