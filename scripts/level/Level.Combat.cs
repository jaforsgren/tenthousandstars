using System;
using Godot;

namespace Tts;

public partial class Level
{
	private void LaunchPlayerTransit(int fromIndex, int toIndex, float fleet)
		=> LaunchTransit(fromIndex, toIndex, fleet, SystemOwner.Player, _ghostFleetOutline,
			(toIdx, f) => ResolvePlayerTransitArrival(toIdx, f));

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
			LaunchTimeSec = Time.GetTicksMsec() / 1000.0,
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
		}
		else
		{
			var defBonus = _defenderBonus * (1f + target.DefenseBonusMultiplier);
			var result = CombatResolver.Resolve(fleet, target.Ships, defBonus);
			if (result.AttackerWins)
			{
				target.Capture(result.AttackerRemainder, SystemOwner.Player);
				SpawnCombatEffect(toIndex, attackerWon: true);
				if (_camera.IsFollowing)
					_camera.FollowSystem(target.GlobalPosition);
			}
			else
			{
				target.SustainDefense(result.DefenderRemainder);
				SpawnCombatEffect(toIndex, attackerWon: false);
			}
		}

		_fogSystem?.Update(_objectiveSystemIndex);
		_gameController.EvaluateEndState();
	}

	private void LaunchAiTransit(int fromIndex, int toIndex, float fleet, AiPlayerData aiPlayer)
	{
		if (_systems[toIndex].OwnerPlayer == SystemOwner.Player)
		{
			PostBark(_barkConfig?.Get("player_under_attack"));
			PostAiBark(aiPlayer);
		}

		var dotColor = _aiColors[aiPlayer.Owner];
		LaunchTransit(fromIndex, toIndex, fleet, aiPlayer.Owner, dotColor,
			(toIdx, f) => ResolveAiTransitArrival(toIdx, f, aiPlayer.Owner, aiPlayer, dotColor));
	}

	private void ResolveAiTransitArrival(int toIndex, float fleet, SystemOwner senderOwner, AiPlayerData aiPlayer, Godot.Color aiOwnerColor)
	{
		var target = _systems[toIndex];

		if (target.OwnerPlayer == senderOwner)
		{
			target.AddFleet(fleet);
		}
		else
		{
			var defBonus = _defenderBonus * (1f + target.DefenseBonusMultiplier);
			var result = CombatResolver.Resolve(fleet, target.Ships, defBonus);
			if (result.AttackerWins)
			{
				target.Capture(result.AttackerRemainder, senderOwner, aiPlayer, aiOwnerColor);
				SpawnCombatEffect(toIndex, attackerWon: true);
				ClearReroute(toIndex);
			}
			else
			{
				target.SustainDefense(result.DefenderRemainder);
				SpawnCombatEffect(toIndex, attackerWon: false);
			}
		}

		OnAiActionTaken();
	}

	private void RegisterTransit(ActiveTransit newTransit)
	{
		var opponent = _transitSystem.Add(newTransit);
		if (opponent == null)
			return;

		var now = Time.GetTicksMsec() / 1000.0;
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
			PostBark(_barkConfig?.Get(playerWon ? "route_combat_win" : "route_combat_lose"));
		}

		if (survivingFleet > 0)
		{
			var now = Time.GetTicksMsec() / 1000.0;
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

	private void PostPlayerTransitBark(int toIndex)
	{
		var pool = _systems[toIndex].OwnerPlayer == SystemOwner.Player
			? _barkConfig?.Get("player_move")
			: _barkConfig?.Get("player_attack");
		PostBark(pool);
	}

	private void PostBark(Bark[]? pool)
	{
		if (pool == null || pool.Length == 0 || _levelUi.ChatWindow == null)
			return;
		var bark = pool[_rng.Next(pool.Length)];
		_levelUi.ChatWindow.PostMessage(bark.Npc, bark.Message);
	}

	private void PostAiBark(AiPlayerData aiPlayer)
	{
		if (_levelUi.ChatWindow == null || aiPlayer.Barks.Length == 0)
			return;
		var message = aiPlayer.Barks[_rng.Next(aiPlayer.Barks.Length)];
		_levelUi.ChatWindow.PostMessage(aiPlayer.FactionName, message);
	}
}
