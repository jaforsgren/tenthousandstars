using System;
using Godot;

namespace Tts;

public partial class Level
{
	private void LaunchPlayerTransit(int fromIndex, int toIndex, float fleet)
	{
		var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
		var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

		var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
		AddChild(transit);

		Func<float, Action> arrivalCallback = f => () =>
		{
			_activeTransits.RemoveAll(t => t.Node == transit);
			ResolvePlayerTransitArrival(toIndex, f);
		};

		transit.Launch(fromEdge, toEdge, _ghostFleetOutline, _transitDurationSeconds, arrivalCallback(fleet));

		RegisterTransit(new ActiveTransit
		{
			FromIndex = fromIndex,
			ToIndex = toIndex,
			Owner = SystemOwner.Player,
			Fleet = fleet,
			LaunchTimeSec = Time.GetTicksMsec() / 1000.0,
			TotalDurationSec = _transitDurationSeconds,
			Node = transit,
			ToWorldPos = toEdge,
			ArrivalCallback = arrivalCallback
		});
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

		UpdateFogOfWar();
		CheckEndCondition();
		CheckDefeatCondition();
	}

	private void LaunchAiTransit(int fromIndex, int toIndex, float fleet, AiPlayerData aiPlayer)
	{
		if (_systems[toIndex].OwnerPlayer == SystemOwner.Player)
		{
			PostBark(_barkConfig?.Get("player_under_attack"));
			PostAiBark(aiPlayer);
		}

		var dotColor = _aiColors[aiPlayer.Owner];
		var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
		var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

		var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
		AddChild(transit);

		Func<float, Action> arrivalCallback = f => () =>
		{
			_activeTransits.RemoveAll(t => t.Node == transit);
			ResolveAiTransitArrival(toIndex, f, aiPlayer.Owner, aiPlayer, dotColor);
		};

		transit.Launch(fromEdge, toEdge, dotColor, _transitDurationSeconds, arrivalCallback(fleet));

		RegisterTransit(new ActiveTransit
		{
			FromIndex = fromIndex,
			ToIndex = toIndex,
			Owner = aiPlayer.Owner,
			Fleet = fleet,
			LaunchTimeSec = Time.GetTicksMsec() / 1000.0,
			TotalDurationSec = _transitDurationSeconds,
			Node = transit,
			ToWorldPos = toEdge,
			ArrivalCallback = arrivalCallback
		});
	}

	private void ResolveAiTransitArrival(int toIndex, float fleet, SystemOwner senderOwner, AiPlayerData aiPlayer, Color aiOwnerColor)
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
		var opponent = _activeTransits.Find(t =>
			t.FromIndex == newTransit.ToIndex &&
			t.ToIndex == newTransit.FromIndex &&
			t.Owner != newTransit.Owner);

		if (opponent != null)
		{
			var now = Time.GetTicksMsec() / 1000.0;
			var remainA = (float)(opponent.TotalDurationSec - (now - opponent.LaunchTimeSec));
			// Both fleets travel the same route at equal speed; meeting time = remainA * D_B / (D_A + D_B)
			var meetingDelay = remainA * newTransit.TotalDurationSec / (opponent.TotalDurationSec + newTransit.TotalDurationSec);
			GetTree().CreateTimer(meetingDelay).Timeout += () => ResolveRouteCombat(opponent, newTransit);
		}

		_activeTransits.Add(newTransit);
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

		_activeTransits.RemoveAll(t => t.Node == a.Node || t.Node == b.Node);
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
				var newArrival = winner.ArrivalCallback(survivingFleet);
				winner.Node.InterruptAndRelaunch(meetingPos, winner.ToWorldPos, winnerRemaining, newArrival);

				RegisterTransit(new ActiveTransit
				{
					FromIndex = winner.FromIndex,
					ToIndex = winner.ToIndex,
					Owner = winner.Owner,
					Fleet = survivingFleet,
					LaunchTimeSec = now,
					TotalDurationSec = winnerRemaining,
					Node = winner.Node,
					ToWorldPos = winner.ToWorldPos,
					ArrivalCallback = winner.ArrivalCallback
				});
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

	private void SpawnCombatEffectAt(Vector2 pos)
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
		if (pool == null || pool.Length == 0 || _chatWindow == null)
			return;
		var bark = pool[_rng.Next(pool.Length)];
		_chatWindow.PostMessage(bark.Npc, bark.Message);
	}

	private void PostAiBark(AiPlayerData aiPlayer)
	{
		if (_chatWindow == null || aiPlayer.Barks.Length == 0)
			return;
		var message = aiPlayer.Barks[_rng.Next(aiPlayer.Barks.Length)];
		_chatWindow.PostMessage(aiPlayer.FactionName, message);
	}
}
