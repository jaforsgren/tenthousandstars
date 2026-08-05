using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Tts.Commitment;
using Tts.Effects;
using Tts.Fleet;
using Tts.Ui;
using Tts.Utils;

namespace Tts.Level;

public partial class Level
{
	private void LaunchPlayerTransit(int fromIndex, int toIndex, float fleet)
		=> LaunchTransit(fromIndex, toIndex, fleet, SystemOwner.Player, _ghostFleetOutline,
			(toIdx, f) => ResolvePlayerTransitArrival(toIdx, f));

	private void LaunchPlayerPathedTransit(List<int> path, float fleet)
		=> LaunchTransit(path[0], path[1], fleet, SystemOwner.Player, _ghostFleetOutline,
			(_, f) => OnPlayerPathedHop(path, 1, f));

	private void OnPlayerPathedHop(List<int> path, int step, float fleet)
	{
		if (step == path.Count - 1)
		{
			ResolvePlayerTransitArrival(path[step], fleet);
			return;
		}
		LaunchTransit(path[step], path[step + 1], fleet, SystemOwner.Player, _ghostFleetOutline,
			(_, f) => OnPlayerPathedHop(path, step + 1, f));
	}

	private const float CapitolShipFleet = 1f;

	private void LaunchCapitolPathedTransit(List<int> path, IntentType intent)
		=> LaunchTransit(path[0], path[1], CapitolShipFleet, SystemOwner.Player, _capitolFill,
			(_, f) => OnCapitolPathedHop(path, 1, intent, f));

	private void OnCapitolPathedHop(List<int> path, int step, IntentType intent, float fleet)
	{
		if (step == path.Count - 1)
		{
			ResolveCapitolArrival(path[step], intent, fleet);
			return;
		}
		LaunchTransit(path[step], path[step + 1], fleet, SystemOwner.Player, _capitolFill,
			(_, f) => OnCapitolPathedHop(path, step + 1, intent, f));
	}

	private void ResolveCapitolArrival(int toIndex, IntentType intent, float fleet)
	{
		var target = _systems[toIndex];
		if (target.IsPlayerOwned)
		{
			target.AddCapitolShip();
			CommitOwnSystem(toIndex, intent);
		}
		else
		{
			_commitmentController.StartCommitment(
				toIndex,
				SystemOwner.Player,
				intent,
				BuildFleetInfluences(fleet),
				TimeUtils.NowSec(),
				target.Ships);
		}
		UpdateFog();
		_gameController.EvaluateEndState();
	}

	private void LaunchTransit(int fromIndex, int toIndex, float fleet, SystemOwner owner, Color dotColor, Action<int, float> onArrival)
	{
		var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
		var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

		var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
		AddChild(transit);

		var at = new ActiveTransit
		{
			FromIndex = fromIndex,
			ToIndex = toIndex,
			Owner = owner,
			Fleet = fleet,
			LaunchTimeSec = TimeUtils.NowSec(),
			TotalDurationSec = _transitDurationSeconds,
			Node = transit,
			ToWorldPos = toEdge
		};

		transit.Arrived += () =>
		{
			_transitSystem.Remove(at);
			onArrival(at.ToIndex, at.Fleet);
		};

		transit.Launch(fromEdge, toEdge, dotColor, _transitDurationSeconds);
		RegisterTransit(at);
	}

	private void ResolvePlayerTransitArrival(int toIndex, float fleet)
	{
		var target = _systems[toIndex];

		if (target.OwnerPlayer == SystemOwner.Player)
		{
			target.AddFleet(fleet);
			UpdateFog();
			_gameController.EvaluateEndState();
			return;
		}

		ResolveDirectCombat(toIndex, fleet);

		UpdateFog();
	}

	private void ResolveDirectCombat(int systemIndex, float fleet)
	{
		var target = _systems[systemIndex];
		var registry = EffectRegistry.Instance;
		var attackerFleet = fleet + (registry?.TotalAttackerStrengthBonus() ?? 0f);
		var defenderBonus = (_defenderBonus + (registry?.TotalDefenderBonusDelta() ?? 0f))
		                    * (1f + target.DefenseBonusMultiplier);
		var result = CombatResolver.Resolve(Math.Max(0f, attackerFleet), target.Ships, Math.Max(0f, defenderBonus));

		if (result.AttackerWins)
		{
			var remainder = Math.Max(0f, result.AttackerRemainder);
			var aiPlayer = _aiPlayers.FirstOrDefault(p => p.Owner == target.OwnerPlayer);
			target.Capture(remainder, SystemOwner.Player);
			SpawnCombatEffect(systemIndex, attackerWon: true);
			if (_camera.IsFollowing)
				_camera.FollowSystem(target.GlobalPosition);
			ClearReroute(systemIndex);
		}
		else
		{
			target.SustainDefense(Math.Max(0f, result.DefenderRemainder));
			SpawnCombatEffect(systemIndex, attackerWon: false);
		}

		PostChat("player_attack");
		UpdateFog();
		_gameController.EvaluateEndState();
	}

	internal void CommitOwnSystem(int systemIndex, IntentType intent)
	{
		_narrativePanel.ShowPreCommitment(intent, () =>
		{
			_commitmentController.StartCommitment(
				systemIndex,
				SystemOwner.Player,
				intent,
				BuildFleetInfluences(_systems[systemIndex].Ships),
				TimeUtils.NowSec());
			UpdateFog();
		});
	}

	private void LaunchAiTransit(int fromIndex, int toIndex, float fleet, AiPlayerData aiPlayer, IntentType intent)
	{
		var dotColor = _aiColors[aiPlayer.Owner];
		LaunchTransit(fromIndex, toIndex, fleet, aiPlayer.Owner, dotColor,
			(toIdx, f) => ResolveAiTransitArrival(toIdx, f, aiPlayer.Owner, aiPlayer, intent));
	}

	private void ResolveAiTransitArrival(int toIndex, float fleet, SystemOwner senderOwner, AiPlayerData aiPlayer, IntentType intent)
	{
		var target = _systems[toIndex];

		if (target.OwnerPlayer == senderOwner)
		{
			target.AddFleet(fleet);
			OnAiActionTaken();
			return;
		}

		// Bark fires on arrival, not on resolution — commitment duration is invisible to the player
		if (target.OwnerPlayer == SystemOwner.Player)
		{
			PostChat("player_under_attack");
			PostAiBark(aiPlayer);
		}

		_commitmentController.StartCommitment(
			toIndex,
			senderOwner,
			intent,
			BuildFleetInfluences(fleet),
			TimeUtils.NowSec(),
			target.Ships);
	}

	private void CommitAiOwnSystem(int systemIndex, AiPlayerData player, IntentType intent)
	{
		_commitmentController.StartCommitment(
			systemIndex,
			player.Owner,
			intent,
			BuildFleetInfluences(_systems[systemIndex].Ships),
			TimeUtils.NowSec());
	}

	private void RegisterTransit(ActiveTransit newTransit)
	{
		var opponent = _transitSystem.Add(newTransit);
		if (opponent == null)
			return;

		var now = TimeUtils.NowSec();
		var remainA = (float)(opponent.TotalDurationSec - (now - opponent.LaunchTimeSec));
		// Both fleets travel the same route at equal speed; meeting time = remainA * D_B / (D_A + D_B)
		var meetingDelay = remainA * newTransit.TotalDurationSec / (opponent.TotalDurationSec + newTransit.TotalDurationSec);
		GetTree().CreateTimer(meetingDelay).Timeout += () => ResolveRouteCombat(opponent, newTransit);
	}

	private void ResolveRouteCombat(ActiveTransit a, ActiveTransit b)
	{
		if (!IsInstanceValid(a.Node) || !IsInstanceValid(b.Node))
			return;

		var meetingPos = a.Node.Position;

		// No defender bonus mid-route: pure simultaneous exchange
		var result = CombatResolver.Resolve(a.Fleet, b.Fleet, 1f);

		var winner = result.AttackerWins ? a : b;
		var loser = result.AttackerWins ? b : a;
		var survivingFleet = result.AttackerWins ? result.AttackerRemainder : result.DefenderRemainder;

		_transitSystem.RemoveByNodes(a.Node, b.Node);
		loser.Node.CancelInFlight();

		SpawnCombatEffectAt(meetingPos);

		if (a.Owner == SystemOwner.Player || b.Owner == SystemOwner.Player)
		{
			var playerWon = winner.Owner == SystemOwner.Player;
			PostChat(playerWon ? "route_combat_win" : "route_combat_lose");
		}

		if (survivingFleet > 0)
		{
			var now = TimeUtils.NowSec();
			var winnerRemaining = (float)(winner.TotalDurationSec - (now - winner.LaunchTimeSec));

			if (winnerRemaining > 0)
			{
				winner.Fleet = survivingFleet;
				winner.LaunchTimeSec = now;
				winner.TotalDurationSec = winnerRemaining;
				winner.Node.InterruptAndRelaunch(meetingPos, winner.ToWorldPos, winnerRemaining);
				RegisterTransit(winner);
			}
			else
			{
				winner.Node.CancelInFlight();
			}
		}
		else
		{
			winner.Node.CancelInFlight();
		}
	}

	private void SpawnCombatEffect(int systemIndex, bool attackerWon)
	{
		SpawnCombatEffectAt(_systems[systemIndex].Position);

		if (!attackerWon)
			return;

		var capture = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(capture);
		capture.Position = _systems[systemIndex].Position;
		capture.PlayCapture();
	}

	private void SpawnCombatEffectAt(Godot.Vector2 pos)
	{
		var impact = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(impact);
		impact.Position = pos;
		impact.PlayImpact();
	}

	// Maps raw ship count to FleetInfluences for any commitment intent.
	// All four values scale with ships so that fleet size affects every intent type.
	// Ratios reflect what a transit fleet is likely good at: strong on aggression/discipline,
	// weaker on curiosity/stability — those scale at lower rates but are not fixed.
	private FleetInfluences BuildFleetInfluences(float ships)
	{
		var perShip = _commitmentConfig.FleetInfluencesPerShip;
		return new FleetInfluences(
			Aggression: ships * perShip.Aggression,
			Discipline: ships * perShip.Discipline,
			Curiosity:  ships * perShip.Curiosity,
			Stability:  ships * perShip.Stability);
	}

	private void PostPlayerTransitBark(int toIndex)
	{
		var tag = _systems[toIndex].OwnerPlayer == SystemOwner.Player ? "player_move" : "player_attack";
		PostChat(tag);
	}

	private void PostAiBark(AiPlayerData aiPlayer)
	{
		if (_levelUi.ChatWindow == null || aiPlayer.Barks.Length == 0)
			return;
		PostMessage(aiPlayer.FactionName, aiPlayer.Barks[_rng.Next(aiPlayer.Barks.Length)]);
	}

	private void PostChat(string poolKey)
	{
		if (_levelUi.ChatWindow == null || !_barkPools.TryGetValue(poolKey, out var pool) || pool.Length == 0)
			return;
		var line = pool[_rng.Next(pool.Length)];
		var sep = line.IndexOf(": ", StringComparison.Ordinal);
		PostMessage(sep > 0 ? line[..sep] : "Commander", sep > 0 ? line[(sep + 2)..] : line);
	}

	private void PostMessage(string speaker, string message)
	{
		_levelUi.ChatWindow?.PostMessage(speaker, message);
	}
}
