using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Tts;

public partial class AiController : Node
{
	private IReadOnlyList<SystemNode> _systems = null!;
	private HashSet<(int, int)> _routeSet = null!;
	private float _defenderBonus;
	private IReadOnlyList<AiPlayerData> _players = null!;
	private AiConfig _config = null!;
	private Random _rng = null!;
	private Action _onActionTaken = null!;
	private Action<int, int, float, AiPlayerData> _launchTransit = null!;
	private double _thinkTimer;
	private Dictionary<int, List<int>> _adjacency = new();
	private readonly List<(int From, int To)> _viableAttacksBuffer = [];
	private readonly List<(int From, int To)> _reinforceOptionsBuffer = [];

	public void Initialize(
		IReadOnlyList<SystemNode> systems,
		HashSet<(int, int)> routeSet,
		float defenderBonus,
		IReadOnlyList<AiPlayerData> players,
		AiConfig config,
		Random rng,
		Action onActionTaken,
		Action<int, int, float, AiPlayerData> launchTransit)
	{
		_systems = systems;
		_routeSet = routeSet;
		_defenderBonus = defenderBonus;
		_players = players;
		_config = config;
		_rng = rng;
		_onActionTaken = onActionTaken;
		_launchTransit = launchTransit;
		_thinkTimer = config.ThinkIntervalSeconds;
		BuildAdjacency();
	}

	private void BuildAdjacency()
	{
		_adjacency = new Dictionary<int, List<int>>(_systems.Count);
		for (var i = 0; i < _systems.Count; i++)
			_adjacency[i] = [];
		foreach (var (from, to) in _routeSet)
		{
			_adjacency[from].Add(to);
			_adjacency[to].Add(from);
		}
	}

	public override void _Process(double delta)
	{
		_thinkTimer -= delta;
		if (_thinkTimer > 0) return;
		_thinkTimer = _config.ThinkIntervalSeconds;

		var acted = false;
		foreach (var player in _players)
		{
			if (player.Disposition == AiDisposition.Dormant) continue;
			if (TryTakeAction(player))
				acted = true;
		}

		if (acted) _onActionTaken();
	}

	private bool TryTakeAction(AiPlayerData player)
	{
		BuildViableAttacks(player, _viableAttacksBuffer);
		BuildReinforceOptions(player, _reinforceOptionsBuffer);

		return player.Disposition switch
		{
			AiDisposition.Aggressive => TryAggressiveAction(player, _viableAttacksBuffer, _reinforceOptionsBuffer),
			AiDisposition.Strategic  => TryStrategicAction(player, _viableAttacksBuffer, _reinforceOptionsBuffer),
			AiDisposition.Cautious   => TryCautiousAction(player, _viableAttacksBuffer, _reinforceOptionsBuffer),
			_ => false
		};
	}

	// Attacks first; reinforces only when no attack is available.
	private bool TryAggressiveAction(AiPlayerData player, List<(int From, int To)> attacks, List<(int From, int To)> reinforcements)
	{
		if (attacks.Count > 0)
			return ExecuteAttack(attacks[_rng.Next(attacks.Count)], player);

		if (reinforcements.Count > 0 && _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(reinforcements[_rng.Next(reinforcements.Count)]);

		return false;
	}

	// Reinforces with high probability before committing to an attack.
	private bool TryStrategicAction(AiPlayerData player, List<(int From, int To)> attacks, List<(int From, int To)> reinforcements)
	{
		if (reinforcements.Count > 0 && _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(reinforcements[_rng.Next(reinforcements.Count)]);

		if (attacks.Count > 0)
			return ExecuteAttack(attacks[_rng.Next(attacks.Count)], player);

		return false;
	}

	// Reinforces with high probability before attacking cautiously.
	private bool TryCautiousAction(AiPlayerData player, List<(int From, int To)> attacks, List<(int From, int To)> reinforcements)
	{
		if (reinforcements.Count > 0 && _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(reinforcements[_rng.Next(reinforcements.Count)]);

		if (attacks.Count > 0)
			return ExecuteAttack(attacks[_rng.Next(attacks.Count)], player);

		return false;
	}

	private void BuildViableAttacks(AiPlayerData player, List<(int From, int To)> buffer)
	{
		buffer.Clear();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner || !_systems[i].HasFleet) continue;
			foreach (var neighbor in GetAdjacentSystemIndices(i))
			{
				if (_systems[neighbor].Owner == player.Owner) continue;
				if (IsViableAttack(i, neighbor, player.Disposition))
					buffer.Add((i, neighbor));
			}
		}
	}

	// Multi-source BFS from all frontline systems back through owned territory.
	// Each rear system records which adjacent owned system is one hop toward the front.
	// Fleets move one hop per tick, naturally pathing through owned systems.
	private void BuildReinforceOptions(AiPlayerData player, List<(int From, int To)> buffer)
	{
		buffer.Clear();
		var frontline = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner) continue;
			var neighbors = GetAdjacentSystemIndices(i);
			for (var j = 0; j < neighbors.Count; j++)
			{
				if (_systems[neighbors[j]].Owner != player.Owner)
				{
					frontline.Add(i);
					break;
				}
			}
		}

		if (frontline.Count == 0) return;

		var visited = new HashSet<int>(frontline);
		var nextHopTowardFront = new Dictionary<int, int>();
		var queue = new Queue<int>(frontline);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var neighbor in GetAdjacentSystemIndices(current))
			{
				if (_systems[neighbor].Owner != player.Owner) continue;
				if (!visited.Add(neighbor)) continue;
				nextHopTowardFront[neighbor] = current;
				queue.Enqueue(neighbor);
			}
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner || !_systems[i].HasFleet) continue;
			if (frontline.Contains(i)) continue;
			if (nextHopTowardFront.TryGetValue(i, out var destination))
				buffer.Add((i, destination));
		}
	}

	private bool IsViableAttack(int fromIndex, int toIndex, AiDisposition disposition)
	{
		var result = CombatResolver.Resolve(_systems[fromIndex].Ships, _systems[toIndex].Ships, _defenderBonus);
		return disposition switch
		{
			AiDisposition.Aggressive => result.AttackerWins,
			AiDisposition.Strategic  => result.AttackerWins && result.AttackerRemainder >= _config.StrategicMinSpareShips,
			AiDisposition.Cautious   => result.AttackerWins && (float)_rng.NextDouble() < _config.CautiousAttackChance,
			_ => false
		};
	}

	private bool ExecuteAttack((int From, int To) option, AiPlayerData player)
	{
		var fleet = _systems[option.From].TakeFleet();
		_launchTransit(option.From, option.To, fleet, player);
		return true;
	}

	private bool ExecuteReinforce((int From, int To) option)
	{
		var fromSystem = _systems[option.From];
		var player = _players.FirstOrDefault(p => p.Owner == fromSystem.Owner);
		if (player == null) return false;
		var fleet = fromSystem.TakeFleet();
		_launchTransit(option.From, option.To, fleet, player);
		return true;
	}

	private List<int> GetAdjacentSystemIndices(int index)
		=> _adjacency.TryGetValue(index, out var neighbors) ? neighbors : [];
}
