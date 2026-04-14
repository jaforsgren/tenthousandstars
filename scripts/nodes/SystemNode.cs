using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

public partial class SystemNode : FogAwareNode
{
	private IReadOnlyList<Planet> _planets = [];
	private readonly List<float> _fleetShips = [];
	private readonly List<FleetNodeBase> _fleetNodes = [];
	private SystemOwner _ownerPlayer;
	private AiPlayerData? _aiPlayerData;
	private Color? _aiOwnerColor;

	private float _systemRadius;
	private float _fleetCircleGap;
	private float _labelWidth;
	private float _baseProduction;
	private Color _planetFill;
	private Color _planetOutline;
	private float _planetOutlineWidth;
	private Color _playerSystemOutline;
	private Color _neutralSystemOutline;

	private SystemCircleNode _systemCircle = null!;
	private readonly List<PlanetNode> _planetNodes = [];
	private SystemUpgrade _upgrade = SystemUpgrade.None;
	private Node2D? _upgradeBadge;
	private float _forgeProductionBonus;
	private float _fortifyDefenseBonusMultiplier;

	private const string ForgeBadgePath = "res://scenes/system/ForgeUpgradeBadge.tscn";
	private const string FortifyBadgePath = "res://scenes/system/FortifyUpgradeBadge.tscn";
	private const string ScenarioBadgePath = "res://scenes/system/ScenarioBadgeNode.tscn";
	private const string PlayerFleetScenePath = "res://scenes/fleet/PlayerFleetNode.tscn";
	private const string NeutralFleetScenePath = "res://scenes/fleet/NeutralFleetNode.tscn";
	private const string AiFleetScenePath = "res://scenes/fleet/AiFleetNode.tscn";
	private const float FleetNodeSpacing = 4f;

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

	private bool _isObjective;
	private bool _isDefend;
	private SystemOwner _targetOwner = SystemOwner.None;
	private float _cachedProductionRate;
	private ScenarioBadgeNode? _scenarioBadge;

	public float ProductionRate => _cachedProductionRate;
	public float DefenseBonusMultiplier => _upgrade == SystemUpgrade.Fortify ? _fortifyDefenseBonusMultiplier : 0f;
	public SystemUpgrade Upgrade => _upgrade;
	public float Ships => _fleetShips.Count > 0 ? _fleetShips.Sum() : 0f;
	public SystemOwner OwnerPlayer => _ownerPlayer;
	public bool HasFleet => _fleetShips.Any(s => s > 0f);
	public bool IsPlayerOwned => _ownerPlayer == SystemOwner.Player;
	public bool IsAiOwned => _ownerPlayer.IsAi();

	public bool ContainsFleetAt(Vector2 worldPos) => GetFleetSlotAt(worldPos) >= 0;

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
		var planetSum = 0f;
		for (var i = 0; i < _planets.Count; i++)
			planetSum += _planets[i].ProductionRate;
		_cachedProductionRate = (planetSum + _baseProduction)
			* (_upgrade == SystemUpgrade.Forge ? 1f + _forgeProductionBonus : 1f);
	}

	public void ApplyUpgrade(SystemUpgrade upgrade)
	{
		_upgrade = upgrade;
		RefreshProductionRate();
		_upgradeBadge?.QueueFree();
		_upgradeBadge = null;

		if (upgrade == SystemUpgrade.None || Engine.IsEditorHint())
			return;

		var scenePath = upgrade == SystemUpgrade.Forge ? ForgeBadgePath : FortifyBadgePath;
		_upgradeBadge = GD.Load<PackedScene>(scenePath).Instantiate<Node2D>();
		AddChild(_upgradeBadge);
		_upgradeBadge.Position = new Vector2(_systemRadius * 0.6f, -_systemRadius * 0.9f);
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
		QueueRedraw();
	}

	public void SustainDefense(float remainingShips)
	{
		// Collapse all fleet slots into one after taking losses
		ClearAllFleets();
		AddFleetSlot(Mathf.Max(0f, remainingShips));
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

		if (_targetOwner != SystemOwner.None && _ownerPlayer == _targetOwner)
			DrawArc(Vector2.Zero, _systemRadius + TargetRingGap, 0f, Mathf.Tau, 64, TargetRingColor, TargetRingWidth);

		if (_ownerPlayer.IsAi() && _aiOwnerColor.HasValue)
			DrawArc(Vector2.Zero, _systemRadius + AiOwnerRingGap, 0f, Mathf.Tau, 64, _aiOwnerColor.Value, AiOwnerRingWidth);
	}

	public void SetSelected(bool selected)
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
			planetNode.Initialize(planet, _planetFill, _planetOutline, _planetOutlineWidth);
			_planetNodes.Add(planetNode);
		}

		if (!Engine.IsEditorHint())
			AddFleetSlot(initialShips);

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
		_planetFill = cfg.PlanetFill.ToColor();
		_planetOutline = cfg.PlanetOutline.ToColor();
		_planetOutlineWidth = cfg.PlanetOutlineWidth;
		var upgradeCfg = ConfigLoader.Load<UpgradeConfig>("res://config/upgrade.json");
		_forgeProductionBonus = upgradeCfg.ForgeProductionBonus;
		_fortifyDefenseBonusMultiplier = upgradeCfg.FortifyDefenseBonusMultiplier;
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

	private FleetNodeBase CreateFleetNode(SystemOwner owner)
	{
		if (owner == SystemOwner.Player)
		{
			var node = GD.Load<PackedScene>(PlayerFleetScenePath).Instantiate<PlayerFleetNode>();
			AddChild(node);
			node.Initialize(_systemRadius, _fleetCircleGap);
			return node;
		}

		if (owner.IsAi() && _aiPlayerData != null && _aiOwnerColor.HasValue)
		{
			var node = GD.Load<PackedScene>(AiFleetScenePath).Instantiate<AiFleetNode>();
			AddChild(node);
			node.Initialize(_systemRadius, _fleetCircleGap, _aiOwnerColor.Value, _aiPlayerData);
			return node;
		}

		var neutral = GD.Load<PackedScene>(NeutralFleetScenePath).Instantiate<NeutralFleetNode>();
		AddChild(neutral);
		neutral.Initialize(_systemRadius, _fleetCircleGap);
		return neutral;
	}
}
