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
		var viableAttacks = BuildAttackOptions(player)
			.Where(o => IsViableAttack(o.From, o.To, player.Disposition))
			.ToList();
		var reinforceOptions = BuildReinforceOptions(player);

		return player.Disposition switch
		{
			AiDisposition.Aggressive => TryAggressiveAction(player, viableAttacks, reinforceOptions),
			AiDisposition.Strategic  => TryStrategicAction(player, viableAttacks, reinforceOptions),
			AiDisposition.Cautious   => TryCautiousAction(player, viableAttacks, reinforceOptions),
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

	private List<(int From, int To)> BuildAttackOptions(AiPlayerData player)
	{
		var options = new List<(int From, int To)>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner || !_systems[i].HasFleet) continue;
			foreach (var neighbor in GetNeighbors(i))
			{
				if (_systems[neighbor].Owner == player.Owner) continue;
				options.Add((i, neighbor));
			}
		}
		return options;
	}

	// Multi-source BFS from all frontline systems back through owned territory.
	// Each rear system records which adjacent owned system is one hop toward the front.
	// Fleets move one hop per tick, naturally pathing through owned systems.
	private List<(int From, int To)> BuildReinforceOptions(AiPlayerData player)
	{
		var frontline = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner) continue;
			if (GetNeighbors(i).Any(n => _systems[n].Owner != player.Owner))
				frontline.Add(i);
		}

		if (frontline.Count == 0) return [];

		var visited = new HashSet<int>(frontline);
		var nextHopTowardFront = new Dictionary<int, int>();
		var queue = new Queue<int>(frontline);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var neighbor in GetNeighbors(current))
			{
				if (_systems[neighbor].Owner != player.Owner) continue;
				if (!visited.Add(neighbor)) continue;
				nextHopTowardFront[neighbor] = current;
				queue.Enqueue(neighbor);
			}
		}

		var options = new List<(int From, int To)>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].Owner != player.Owner || !_systems[i].HasFleet) continue;
			if (frontline.Contains(i)) continue;
			if (nextHopTowardFront.TryGetValue(i, out var destination))
				options.Add((i, destination));
		}
		return options;
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
		var player = _players.First(p => p.Owner == fromSystem.Owner);
		var fleet = fromSystem.TakeFleet();
		_launchTransit(option.From, option.To, fleet, player);
		return true;
	}

	private IEnumerable<int> GetNeighbors(int index)
	{
		foreach (var (from, to) in _routeSet)
		{
			if (from == index) yield return to;
			else if (to == index) yield return from;
		}
	}
}
