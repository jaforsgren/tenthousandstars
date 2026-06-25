using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Tts.Commitment;
using Tts.Config;
using Tts.Level;
using Tts.Nodes;
using Tts.Utils;

namespace Tts.Ai;

public partial class AiController : Node
{
	[Signal]
	public delegate void ActionTakenEventHandler();

	private IReadOnlyList<SystemNode> _systems = null!;
	private HashSet<(int, int)> _routeSet = null!;
	private float _defenderBonus;
	private IReadOnlyList<AiPlayerData> _players = null!;
	private AiConfig _config = null!;
	private CommitmentConfig _commitmentConfig = null!;
	private CommitmentController _commitmentController = null!;
	private Random _rng = null!;
	private Action<int, int, float, AiPlayerData, IntentType> _launchTransit = null!;
	private Action<int, AiPlayerData, IntentType> _commitDirect = null!;
	private double _thinkTimer;
	private Dictionary<int, List<int>> _adjacency = new();
	private readonly List<(int From, int To)> _viableAttacksBuffer  = [];
	private readonly List<(int From, int To)> _viableContestsBuffer = [];
	private readonly List<(int From, int To)> _reinforceOptionsBuffer = [];

	public void Initialize(
		IReadOnlyList<SystemNode> systems,
		HashSet<(int, int)> routeSet,
		float defenderBonus,
		IReadOnlyList<AiPlayerData> players,
		AiConfig config,
		CommitmentConfig commitmentConfig,
		CommitmentController commitmentController,
		Random rng,
		Action<int, int, float, AiPlayerData, IntentType> launchTransit,
		Action<int, AiPlayerData, IntentType> commitDirect)
	{
		_systems = systems;
		_routeSet = routeSet;
		_defenderBonus = defenderBonus;
		_players = players;
		_config = config;
		_commitmentConfig = commitmentConfig;
		_commitmentController = commitmentController;
		_rng = rng;
		_launchTransit = launchTransit;
		_commitDirect = commitDirect;
		_thinkTimer = config.ThinkIntervalSeconds;
		_adjacency = GraphUtils.BuildAdjacency(_systems.Count, _routeSet);
	}

	public override void _Process(double delta)
	{
		_thinkTimer -= delta;
		if (_thinkTimer > 0) return;
		_thinkTimer = _config.ThinkIntervalSeconds;

		EvaluateInterrupts();

		var acted = false;
		foreach (var player in _players)
		{
			if (player.Disposition == AiDisposition.Dormant) continue;
			if (TryTakeAction(player))
				acted = true;
		}

		if (acted) EmitSignal(SignalName.ActionTaken);
	}

	private void EvaluateInterrupts()
	{
		foreach (var c in _commitmentController.GetAllActive())
		{
			if (c.IsComplete || c.IsInterrupted || !c.Owner.IsAi()) continue;
			var visible = _commitmentController.GetVisibleState(c.SystemIndex);
			if (AiCommitmentEvaluator.ShouldInterrupt(c, visible, _commitmentConfig))
				_commitmentController.InterruptCommitment(c.Id);
		}
	}

	private bool TryTakeAction(AiPlayerData player)
	{
		BuildViableAttacks(player, _viableAttacksBuffer);
		BuildViableContests(player, _viableContestsBuffer);
		BuildReinforceOptions(player, _reinforceOptionsBuffer);

		var transitIntent = PickTransitIntent(player);

		var acted = player.Disposition switch
		{
			AiDisposition.Aggressive => TryAggressiveAction(player, transitIntent),
			AiDisposition.Strategic  => TryStrategicAction(player, transitIntent),
			AiDisposition.Cautious   => TryCautiousAction(player, transitIntent),
			_                        => false
		};

		if (!acted)
			acted = TryDirectCommit(player);

		return acted;
	}

	// Attacks first; reinforces only when no attack is available.
	private bool TryAggressiveAction(AiPlayerData player, IntentType transitIntent)
	{
		var targets = PickTransitTargets(transitIntent);
		if (targets.Count > 0)
			return ExecuteTransit(targets[_rng.Next(targets.Count)], player, transitIntent);

		if (_reinforceOptionsBuffer.Count > 0
			&& _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(_reinforceOptionsBuffer[_rng.Next(_reinforceOptionsBuffer.Count)]);

		return false;
	}

	// Reinforces with high probability before committing to a transit action.
	private bool TryStrategicAction(AiPlayerData player, IntentType transitIntent)
	{
		if (_reinforceOptionsBuffer.Count > 0
			&& _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(_reinforceOptionsBuffer[_rng.Next(_reinforceOptionsBuffer.Count)]);

		var targets = PickTransitTargets(transitIntent);
		if (targets.Count > 0)
			return ExecuteTransit(targets[_rng.Next(targets.Count)], player, transitIntent);

		return false;
	}

	// Reinforces before attacking cautiously.
	private bool TryCautiousAction(AiPlayerData player, IntentType transitIntent)
	{
		if (_reinforceOptionsBuffer.Count > 0
			&& _rng.NextDouble() < _config.DispositionReinforceChance[player.Disposition.ToString()])
			return ExecuteReinforce(_reinforceOptionsBuffer[_rng.Next(_reinforceOptionsBuffer.Count)]);

		var targets = PickTransitTargets(transitIntent);
		if (targets.Count > 0)
			return ExecuteTransit(targets[_rng.Next(targets.Count)], player, transitIntent);

		return false;
	}

	// Evaluates direct (no-transit) commitments on own systems: Fortify, Investigate, Exploit.
	private bool TryDirectCommit(AiPlayerData player)
	{
		var decision = AiCommitmentEvaluator.Evaluate(
			player.Owner,
			player.Disposition,
			_systems,
			_commitmentController.GetAllActive(),
			idx => _commitmentController.GetVisibleState(idx),
			_commitmentConfig,
			_rng);

		if (decision == null) return false;
		if (!IsDirectIntent(decision.Value.Intent)) return false;
		if (!_systems[decision.Value.SystemIndex].HasFleet) return false;

		// Investigate on enemy systems would require a transit — skip for direct path
		if (decision.Value.Intent == IntentType.Investigate
			&& _systems[decision.Value.SystemIndex].OwnerPlayer != player.Owner)
			return false;

		_commitDirect(decision.Value.SystemIndex, player, decision.Value.Intent);
		return true;
	}

	private void BuildViableAttacks(AiPlayerData player, List<(int From, int To)> buffer)
	{
		buffer.Clear();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].OwnerPlayer != player.Owner || !_systems[i].HasFleet) continue;
			foreach (var neighbor in GetAdjacentSystemIndices(i))
			{
				if (_systems[neighbor].OwnerPlayer == player.Owner) continue;
				if (IsViableAttack(i, neighbor, player.Disposition))
					buffer.Add((i, neighbor));
			}
		}
	}

	// Contest has a lower bar than Attack — any adjacent enemy system is valid.
	private void BuildViableContests(AiPlayerData player, List<(int From, int To)> buffer)
	{
		buffer.Clear();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].OwnerPlayer != player.Owner || !_systems[i].HasFleet) continue;
			foreach (var neighbor in GetAdjacentSystemIndices(i))
			{
				if (_systems[neighbor].OwnerPlayer == player.Owner) continue;
				buffer.Add((i, neighbor));
			}
		}
	}

	// Multi-source BFS from all frontline systems back through owned territory.
	// Each rear system records which adjacent owned system is one hop toward the front.
	// Fleets move one hop per tick, naturally pathing through owned systems
	// TODO: cache this, we do not need to rebuild this each tick
	private void BuildReinforceOptions(AiPlayerData player, List<(int From, int To)> buffer)
	{
		buffer.Clear();
		var frontline = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].OwnerPlayer != player.Owner) continue;
			var neighbors = GetAdjacentSystemIndices(i);
			for (var j = 0; j < neighbors.Count; j++)
			{
				if (_systems[neighbors[j]].OwnerPlayer != player.Owner)
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
				if (_systems[neighbor].OwnerPlayer != player.Owner) continue;
				if (!visited.Add(neighbor)) continue;
				nextHopTowardFront[neighbor] = current;
				queue.Enqueue(neighbor);
			}
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].OwnerPlayer != player.Owner || !_systems[i].HasFleet) continue;
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

	// Weighted pick between Attack and Contest based on disposition weights.
	// Falls back to Attack if weights are missing or zero.
	private IntentType PickTransitIntent(AiPlayerData player)
	{
		if (!_commitmentConfig.Ai.DispositionIntentWeights.TryGetValue(player.Disposition.ToString(), out var weights))
			return IntentType.Attack;

		var attackWeight  = weights.GetValueOrDefault("Attack",  0f);
		var contestWeight = weights.GetValueOrDefault("Contest", 0f);
		var total = attackWeight + contestWeight;
		if (total <= 0f) return IntentType.Attack;

		return (float)_rng.NextDouble() * total < attackWeight ? IntentType.Attack : IntentType.Contest;
	}

	private List<(int From, int To)> PickTransitTargets(IntentType intent)
		=> intent == IntentType.Contest && _viableContestsBuffer.Count > 0
			? _viableContestsBuffer
			: _viableAttacksBuffer;

	private static bool IsDirectIntent(IntentType intent)
		=> intent is IntentType.Fortify or IntentType.Investigate or IntentType.Exploit;

	private bool ExecuteTransit((int From, int To) option, AiPlayerData player, IntentType intent)
	{
		var fleet = _systems[option.From].TakeFleet();
		_launchTransit(option.From, option.To, fleet, player, intent);
		return true;
	}

	private bool ExecuteReinforce((int From, int To) option)
	{
		var fromSystem = _systems[option.From];
		var player = _players.FirstOrDefault(p => p.Owner == fromSystem.OwnerPlayer);
		if (player == null) return false;
		var fleet = fromSystem.TakeFleet();
		// Intent is irrelevant for reinforcement — fleet joins own system on arrival
		_launchTransit(option.From, option.To, fleet, player, IntentType.Attack);
		return true;
	}

	public string GetDebugState()
	{
		var parts = new List<string>();
		foreach (var player in _players)
		{
			var owned = 0;
			var fleet = 0f;
			for (var i = 0; i < _systems.Count; i++)
			{
				if (_systems[i].OwnerPlayer != player.Owner) continue;
				owned++;
				fleet += _systems[i].Ships;
			}
			parts.Add($"{player.Owner}({player.Disposition.ToString()[..3]}) sys:{owned} fl:{fleet:F0}");
		}
		return string.Join("  ", parts);
	}

	private List<int> GetAdjacentSystemIndices(int index)
		=> _adjacency.TryGetValue(index, out var neighbors) ? neighbors : [];
}
