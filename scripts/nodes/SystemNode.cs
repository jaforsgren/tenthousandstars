using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Tts.Config;
using Tts.Fleet;
using Tts.Level;
using Tts.Types;
using Tts.Utils;

namespace Tts.Nodes;

public partial class SystemNode : FogAwareNode
{
	private IReadOnlyList<Planet> _planets = [];
	private readonly List<float> _fleetShips = [];
	private readonly List<FleetNode> _fleetNodes = [];
	private SystemOwner _ownerPlayer;
	private AiPlayerData? _aiPlayerData;
	private Color? _aiOwnerColor;

	private float _systemRadius;
	private float _fleetCircleGap;
	private float _labelWidth;
	private float _baseProduction;
	private Color _playerSystemOutline;
	private Color _neutralSystemOutline;
	private PlanetGradient[] _planetGradients = [];
	private float _planetOrbitSpeed;

	private SystemCircleNode _systemCircle = null!;
	private readonly List<PlanetNode> _planetNodes = [];
	private SystemUpgrade _upgrade = SystemUpgrade.None;
	private FleetNode? _capitolShipNode;
	private float _forgeProductionBonus;
	private float _fortifyDefenseBonusMultiplier;

	private const string ScenarioBadgePath = "res://scenes/system/ScenarioBadgeNode.tscn";
	private const string ProductionArcScenePath = "res://scenes/system/ProductionArcNode.tscn";
	private const string FleetScenePath = "res://scenes/fleet/FleetNode.tscn";
	private const float FleetNodeSpacing = 4f;

	private static readonly Color ForgeBadgeColor   = new(1f,  0.55f, 0.1f, 0.95f);
	private static readonly Color FortifyBadgeColor = new(0.2f, 0.6f, 1f,  0.95f);
	private const float BadgeRadius = 8f;

	private static readonly Color ObjectiveRingColor = new(1f, 0.85f, 0.2f, 0.8f);
	private const float ObjectiveRingGap = 5f;
	private const float ObjectiveRingWidth = 1.5f;
	private static readonly Color TargetRingColor = new(1f, 0.25f, 0.2f, 0.85f);
	private const float TargetRingGap = 10f;
	private const float TargetRingWidth = 1.5f;
	private static readonly Color DefendRingColor = new(0.3f, 0.7f, 1f, 0.85f);
	private const float DefendRingGap = 10f;
	private const float DefendRingWidth = 1.5f;
	private const float AiOwnerRingGap = 2f;
	private const float AiOwnerRingWidth = 1.5f;
	private static readonly Color EncounterRingColor = new(1f, 0.65f, 0.1f, 0.85f);
	private const float EncounterRingGap = 7f;
	private const float EncounterRingWidth = 1.5f;

	private bool _isObjective;
	private bool _isDefend;
	private bool _hasEncounterMark;
	private SystemOwner _targetOwner = SystemOwner.None;
	private float _cachedProductionRate;
	private ScenarioBadgeNode? _scenarioBadge;
	private ProductionArcNode? _productionArc;
	private float _lastShipsForArc;

	public bool HasCapitolShip => _capitolShipNode != null;

	public float ProductionRate => _cachedProductionRate;
	public float DefenseBonusMultiplier => _upgrade == SystemUpgrade.Fortify ? _fortifyDefenseBonusMultiplier : 0f;
	public SystemUpgrade Upgrade => _upgrade;
	public float Ships => _fleetShips.Count > 0 ? _fleetShips.Sum() : 0f;
	public SystemOwner OwnerPlayer => _ownerPlayer;
	public bool HasFleet => _fleetShips.Any(s => s > 0f);
	public bool IsPlayerOwned => _ownerPlayer == SystemOwner.Player;
	public bool IsAiOwned => _ownerPlayer.IsAi();

	public bool ContainsFleetAt(Vector2 worldPos) => GetFleetSlotAt(worldPos) >= 0;

	public bool ContainsCapitolShipAt(Vector2 worldPos)
		=> _capitolShipNode != null && _capitolShipNode.ContainsPoint(worldPos);

	public void AddCapitolShip()
	{
		if (_capitolShipNode != null) return;
		var node = GD.Load<PackedScene>(FleetScenePath).Instantiate<FleetNode>();
		AddChild(node);
		node.InitializeCapitol(_systemRadius, _fleetCircleGap);
		_capitolShipNode = node;
	}

	public void TakeCapitolShip()
	{
		_capitolShipNode?.QueueFree();
		_capitolShipNode = null;
	}

	public void SetCapitolShipVisible(bool visible)
	{
		if (_capitolShipNode != null)
			_capitolShipNode.Visible = visible;
	}

	public int GetFleetSlotAt(Vector2 worldPos)
	{
		for (var i = 0; i < _fleetNodes.Count; i++)
			if (_fleetNodes[i].ContainsPoint(worldPos)) return i;
		return -1;
	}

	public float GetFleetShips(int slot) =>
		slot >= 0 && slot < _fleetShips.Count ? _fleetShips[slot] : 0f;

	public bool ContainsSystemAt(Vector2 worldPos)
		=> worldPos.DistanceTo(GlobalPosition) <= _systemRadius;

	public float TakeFleet(int slot = 0)
	{
		if (slot < 0 || slot >= _fleetShips.Count) return 0f;
		var taken = _fleetShips[slot];
		_fleetShips.RemoveAt(slot);
		_fleetNodes[slot].QueueFree();
		_fleetNodes.RemoveAt(slot);
		RepositionFleetNodes();
		return taken;
	}

	public void AddFleet(float ships)
	{
		if (_fleetShips.Count == 0)
		{
			AddFleetSlot(ships);
			return;
		}
		_fleetShips[0] += ships;
		_fleetNodes[0].UpdateFleet(_fleetShips[0]);
	}

	public void SplitFleet(int slot)
	{
		if (slot < 0 || slot >= _fleetShips.Count) return;
		var half = _fleetShips[slot] / 2f;
		_fleetShips[slot] = half;
		_fleetNodes[slot].UpdateFleet(half);
		AddFleetSlot(half);
	}

	public void SpendShips(float amount)
	{
		if (_fleetShips.Count == 0) return;
		_fleetShips[0] = Mathf.Max(0f, _fleetShips[0] - amount);
		_fleetNodes[0].UpdateFleet(_fleetShips[0]);
	}

	private void RefreshProductionRate()
	{
		_cachedProductionRate = (_planets.Sum(p => p.ProductionRate) + _baseProduction)
			* (_upgrade == SystemUpgrade.Forge ? 1f + _forgeProductionBonus : 1f);
	}

	public void ApplyUpgrade(SystemUpgrade upgrade)
	{
		_upgrade = upgrade;
		RefreshProductionRate();
		QueueRedraw();
	}

	public void Capture(float ships, SystemOwner newOwner, AiPlayerData? aiPlayer = null, Color? aiOwnerColor = null)
	{
		_ownerPlayer = newOwner;
		_aiPlayerData = aiPlayer;
		_aiOwnerColor = aiOwnerColor;
		_systemCircle.SetOutline(newOwner == SystemOwner.None ? _neutralSystemOutline : _playerSystemOutline);
		ApplyUpgrade(SystemUpgrade.None);
		ClearAllFleets();
		AddFleetSlot(ships);

		_productionArc?.QueueFree();
		_productionArc = null;
		if (newOwner == SystemOwner.Player)
		{
			SpawnProductionArc(ships);
			AddCapitolShip();
		}
		else
		{
			TakeCapitolShip();
		}

		QueueRedraw();
	}

	public void SustainDefense(float remainingShips)
	{
		// Collapse all fleet slots into one after taking losses
		var clamped = Mathf.Max(0f, remainingShips);
		ClearAllFleets();
		AddFleetSlot(clamped);
		_lastShipsForArc = clamped;
	}

	public void MarkAsObjective()
	{
		_isObjective = true;
		QueueRedraw();
	}

	public void MarkAsDefend()
	{
		_isDefend = true;
		QueueRedraw();
	}

	public void SetTargetOwner(SystemOwner owner)
	{
		_targetOwner = owner;
		QueueRedraw();
	}

	public void SetEncounterMark(bool has)
	{
		_hasEncounterMark = has;
		QueueRedraw();
	}

	public void SetScenarioBadge(bool show)
	{
		if (show && _scenarioBadge == null)
		{
			_scenarioBadge = GD.Load<PackedScene>(ScenarioBadgePath).Instantiate<ScenarioBadgeNode>();
			AddChild(_scenarioBadge);
			_scenarioBadge.Initialize(_systemRadius, _fleetCircleGap);
		}
		else if (!show && _scenarioBadge != null)
		{
			_scenarioBadge.QueueFree();
			_scenarioBadge = null;
		}
	}

	public override void _Draw()
	{
		if (_isObjective)
			DrawArc(Vector2.Zero, _systemRadius + ObjectiveRingGap, 0f, Mathf.Tau, 64, ObjectiveRingColor, ObjectiveRingWidth);

		if (_isDefend)
			DrawArc(Vector2.Zero, _systemRadius + DefendRingGap, 0f, Mathf.Tau, 64, DefendRingColor, DefendRingWidth);

		if (_hasEncounterMark)
			DrawArc(Vector2.Zero, _systemRadius + EncounterRingGap, 0f, Mathf.Tau, 64, EncounterRingColor, EncounterRingWidth);

		if (_targetOwner != SystemOwner.None && _ownerPlayer == _targetOwner)
			DrawArc(Vector2.Zero, _systemRadius + TargetRingGap, 0f, Mathf.Tau, 64, TargetRingColor, TargetRingWidth);

		if (_ownerPlayer.IsAi() && _aiOwnerColor.HasValue)
			DrawArc(Vector2.Zero, _systemRadius + AiOwnerRingGap, 0f, Mathf.Tau, 64, _aiOwnerColor.Value, AiOwnerRingWidth);

		if (_upgrade != SystemUpgrade.None)
		{
			var badgePos = new Vector2(_systemRadius * 0.6f, -_systemRadius * 0.9f);
			var badgeColor = _upgrade == SystemUpgrade.Forge ? ForgeBadgeColor : FortifyBadgeColor;
			var letter = _upgrade == SystemUpgrade.Forge ? "F" : "D";
			DrawCircle(badgePos, BadgeRadius, badgeColor);
			DrawString(ThemeDB.FallbackFont, badgePos + new Vector2(-4f, 4f), letter,
				HorizontalAlignment.Left, -1, 10, Colors.Black);
		}
	}

	public void RefreshFleetVisuals()
	{
		for (var i = 0; i < _fleetNodes.Count; i++)
			_fleetNodes[i].UpdateFleet(_fleetShips[i]);
	}

	public void Initialize(IReadOnlyList<Planet> planets, SystemOwner owner, float initialShips = 0f, AiPlayerData? aiPlayer = null, Color? aiOwnerColor = null)
	{
		_planets = planets;
		_ownerPlayer = owner;
		_aiPlayerData = aiPlayer;
		_aiOwnerColor = aiOwnerColor;
		RefreshProductionRate();

		if (owner != SystemOwner.None)
			_systemCircle.SetOutline(_playerSystemOutline);

		foreach (var planet in _planets)
		{
			var planetNode = new PlanetNode();
			AddChild(planetNode);
			var gi = Math.Abs((int)(planet.OrbitAngle * 10000f)) % _planetGradients.Length;
			var gradient = _planetGradients[gi];
			planetNode.Initialize(planet, gradient.Inner.ToColor(), gradient.Outer.ToColor(), _planetOrbitSpeed);
			_planetNodes.Add(planetNode);
		}

		if (!Engine.IsEditorHint())
		{
			AddFleetSlot(initialShips);
			if (owner == SystemOwner.Player)
			{
				SpawnProductionArc(initialShips);
				AddCapitolShip();
			}
		}

		if (_ownerPlayer.IsAi())
			QueueRedraw();
	}

	public override void _Ready()
	{
		var cfg = ConfigLoader.Load<SystemConfig>("res://config/system.json");
		_systemRadius = cfg.SystemRadius;
		_fleetCircleGap = cfg.FleetCircleGap;
		_labelWidth = cfg.LabelWidth;
		_baseProduction = cfg.BaseProduction;
		_playerSystemOutline = cfg.SystemOutline.ToColor();
		_neutralSystemOutline = cfg.NeutralSystemOutline.ToColor();
		_planetGradients = cfg.PlanetGradients;
		_planetOrbitSpeed = cfg.PlanetOrbitSpeed;
		var levelCfg = ConfigLoader.Load<LevelConfig>("res://config/level.json");
		_forgeProductionBonus = levelCfg.ForgeProductionBonus;
		_fortifyDefenseBonusMultiplier = levelCfg.FortifyDefenseBonusMultiplier;
		_systemCircle = new SystemCircleNode();
		AddChild(_systemCircle);
		// Default to neutral outline; Initialize() updates it once the owner is known
		_systemCircle.Initialize(
			_systemRadius,
			cfg.SystemFill.ToColor(),
			_neutralSystemOutline,
			cfg.SystemOutlineWidth
		);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint() || _ownerPlayer == SystemOwner.None)
			return;

		if (_fleetShips.Count == 0)
			AddFleetSlot(0f);

		_fleetShips[0] += ProductionRate * (float)delta;
		_fleetNodes[0].UpdateFleet(_fleetShips[0]);

		if (_productionArc != null)
		{
			var ships = _fleetShips[0];
			if (Mathf.FloorToInt(ships) > Mathf.FloorToInt(_lastShipsForArc))
				_productionArc.PlayProduced();
			_lastShipsForArc = ships;
			_productionArc.SetProgress(ships - Mathf.Floor(ships));
		}
	}

	private void AddFleetSlot(float ships)
	{
		var node = CreateFleetNode(_ownerPlayer);
		node.UpdateFleet(ships);
		_fleetShips.Add(ships);
		_fleetNodes.Add(node);
		RepositionFleetNodes();
	}

	private void ClearAllFleets()
	{
		foreach (var node in _fleetNodes)
			node.QueueFree();
		_fleetNodes.Clear();
		_fleetShips.Clear();
	}

	private void RepositionFleetNodes()
	{
		var count = _fleetNodes.Count;
		if (count == 0) return;
		var step = _labelWidth + FleetNodeSpacing;
		var totalWidth = (count - 1) * step;
		for (var i = 0; i < count; i++)
		{
			var pos = _fleetNodes[i].Position;
			_fleetNodes[i].Position = new Vector2(-totalWidth / 2f + i * step, pos.Y);
		}
	}

	private FleetNode CreateFleetNode(SystemOwner owner)
	{
		var node = GD.Load<PackedScene>(FleetScenePath).Instantiate<FleetNode>();
		AddChild(node);
		if (owner == SystemOwner.Player)
			node.InitializePlayer(_systemRadius, _fleetCircleGap);
		else if (owner.IsAi() && _aiPlayerData != null && _aiOwnerColor.HasValue)
			node.InitializeAi(_systemRadius, _fleetCircleGap, _aiOwnerColor.Value, _aiPlayerData.FactionName);
		else
			node.InitializeNeutral(_systemRadius, _fleetCircleGap);
		return node;
	}

	private void SpawnProductionArc(float currentShips)
	{
		_productionArc = GD.Load<PackedScene>(ProductionArcScenePath).Instantiate<ProductionArcNode>();
		AddChild(_productionArc);
		_productionArc.Initialize(_systemRadius);
		_lastShipsForArc = currentShips;
		_productionArc.SetProgress(currentShips - Mathf.Floor(currentShips));
	}
}
