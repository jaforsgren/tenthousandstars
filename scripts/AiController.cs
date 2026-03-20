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
	private double _thinkTimer;

	public void Initialize(
		IReadOnlyList<SystemNode> systems,
		HashSet<(int, int)> routeSet,
		float defenderBonus,
		IReadOnlyList<AiPlayerData> players,
		AiConfig config,
		Random rng,
		Action onActionTaken)
	{
		_systems = systems;
		_routeSet = routeSet;
		_defenderBonus = defenderBonus;
		_players = players;
		_config = config;
		_rng = rng;
		_onActionTaken = onActionTaken;
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

		if (options.Count == 0) return false;

		var viable = options.Where(o => IsViable(o.From, o.To, player.Disposition)).ToList();
		if (viable.Count == 0) return false;

		var chosen = viable[_rng.Next(viable.Count)];
		ExecuteAttack(chosen.From, chosen.To, player);
		return true;
	}

	private bool IsViable(int fromIndex, int toIndex, AiDisposition disposition)
	{
		var attackerFleet = _systems[fromIndex].Ships;
		var defenderFleet = _systems[toIndex].Ships;
		var result = CombatResolver.Resolve(attackerFleet, defenderFleet, _defenderBonus);

		return disposition switch
		{
			AiDisposition.Aggressive => result.AttackerWins,
			AiDisposition.Strategic => result.AttackerWins && result.AttackerRemainder >= _config.StrategicMinSpareShips,
			AiDisposition.Cautious => result.AttackerWins && (float)_rng.NextDouble() < _config.CautiousAttackChance,
			_ => false
		};
	}

	private void ExecuteAttack(int fromIndex, int toIndex, AiPlayerData player)
	{
		var attacker = _systems[fromIndex];
		var target = _systems[toIndex];
		var attackerFleet = attacker.TakeFleet();
		var dispositionColor = _config.DispositionColors[player.Disposition.ToString()].ToColor();

		var result = CombatResolver.Resolve(attackerFleet, target.Ships, _defenderBonus);
		if (result.AttackerWins)
			target.Capture(result.AttackerRemainder, player.Owner, player, dispositionColor);
		else
			target.SustainDefense(result.DefenderRemainder);
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
