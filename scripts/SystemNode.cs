using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

public partial class SystemNode : Node2D
{
	private IReadOnlyList<Planet> _planets = [];
	private float _ships;
	private SystemOwner _owner;
	private bool _selected;

	private float _systemRadius;
	private float _fleetCircleRadius;
	private float _fleetCircleGap;
	private float _baseProduction;
	private Color _planetFill;
	private Color _planetOutline;
	private float _planetOutlineWidth;

	private SystemCircleNode _systemCircle = null!;
	private FleetNode _fleetNode = null!;
	private readonly List<PlanetNode> _planetNodes = [];

	private static readonly Color ScoutedModulate = new(0.5f, 0.55f, 0.65f, 0.45f);

	private FogState _fogState = FogState.Revealed;

	public float ProductionRate => _planets.Sum(p => p.ProductionRate) + _baseProduction;
	public float Ships => _ships;
	public SystemOwner Owner => _owner;
	public FogState FogState => _fogState;
	public bool HasFleet => _ships > 0;
	public bool IsPlayerOwned => _owner == SystemOwner.Player;

	public bool ContainsFleetAt(Vector2 worldPos) => _fleetNode?.ContainsPoint(worldPos) ?? false;

	public bool ContainsSystemAt(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= _systemRadius;

	public int? PlanetIndexAt(Vector2 worldPos)
	{
		for (var i = 0; i < _planetNodes.Count; i++)
		{
			if (_planetNodes[i].ContainsPoint(worldPos))
				return i;
		}
		return null;
	}

	public float TakeFleet()
	{
		var taken = _ships;
		_ships = 0f;
		_selected = false;
		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
		return taken;
	}

	public void AddFleet(float ships)
	{
		_ships += ships;
		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
	}

	public void Capture(float ships, SystemOwner newOwner)
	{
		_owner = newOwner;
		_ships = ships;
		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
	}

	public void SustainDefense(float remainingShips)
	{
		_ships = Mathf.Max(0f, remainingShips);
		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
	}

	public void SetSelected(bool selected)
	{
		_selected = selected;
		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
	}

	public void SetFogState(FogState fogState)
	{
		_fogState = fogState;
		Visible = fogState != FogState.Hidden;
		Modulate = fogState == FogState.Scouted ? ScoutedModulate : Colors.White;
	}

	public void Initialize(IReadOnlyList<Planet> planets, SystemOwner owner, float initialShips = 0f)
	{
		_planets = planets;
		_owner = owner;
		_ships = initialShips;

		foreach (var planet in _planets)
		{
			var planetNode = new PlanetNode();
			AddChild(planetNode);
			planetNode.Initialize(planet, _planetFill, _planetOutline, _planetOutlineWidth);
			_planetNodes.Add(planetNode);
		}

		_fleetNode?.UpdateFleet(_ships, _owner, _selected);
	}

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_systemRadius = cfg.SystemRadius;
		_fleetCircleRadius = cfg.FleetCircleRadius;
		_fleetCircleGap = cfg.FleetCircleGap;
		_baseProduction = cfg.BaseProduction;
		_planetFill = cfg.PlanetFill.ToColor();
		_planetOutline = cfg.PlanetOutline.ToColor();
		_planetOutlineWidth = cfg.PlanetOutlineWidth;

		_systemCircle = new SystemCircleNode();
		AddChild(_systemCircle);
		_systemCircle.Initialize(
			_systemRadius,
			cfg.SystemFill.ToColor(),
			cfg.SystemOutline.ToColor(),
			cfg.SystemOutlineWidth
		);

		if (Engine.IsEditorHint())
			return;

		_fleetNode = new FleetNode();
		AddChild(_fleetNode);
		_fleetNode.Initialize(
			_systemRadius,
			_fleetCircleGap,
			_fleetCircleRadius,
			cfg.LabelWidth,
			cfg.LabelHeight,
			cfg.FleetFill.ToColor(),
			cfg.FleetOutline.ToColor(),
			cfg.NeutralFleetFill.ToColor(),
			cfg.NeutralFleetOutline.ToColor(),
			cfg.FleetOutlineWidth
		);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _owner == SystemOwner.None)
			return;

		_ships += ProductionRate * (float)delta;
		_fleetNode.UpdateFleet(_ships, _owner, _selected);
	}
}
