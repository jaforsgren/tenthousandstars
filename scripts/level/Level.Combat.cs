using System;
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
			UpdateFog();
			_gameController.EvaluateEndState();
			return;
		}

		_pendingArrival = (toIndex, fleet);
		_levelUi.SystemActionMenu.ShowIntentOnly(
			target.GlobalPosition,
			[("Attack", IntentType.Attack), ("Contest", IntentType.Contest)],
			CommitPendingArrival,
			required: true);
		UpdateFog();
	}

	private void CommitPendingArrival(IntentType intent)
	{
		if (_pendingArrival is not { } arrival) return;
		_commitmentDialogue.ShowPreCommitment(intent, () =>
		{
			_pendingArrival = null;
			var target = _systems[arrival.SystemIndex];
			_commitmentController.StartCommitment(
				arrival.SystemIndex,
				SystemOwner.Player,
				intent,
				BuildFleetInfluences(arrival.Fleet),
				Time.GetTicksMsec() / 1000.0,
				target.Ships);
			UpdateFog();
		});
	}

	internal void CommitOwnSystem(int systemIndex, IntentType intent)
	{
		_commitmentDialogue.ShowPreCommitment(intent, () =>
		{
			_commitmentController.StartCommitment(
				systemIndex,
				SystemOwner.Player,
				intent,
				BuildFleetInfluences(_systems[systemIndex].Ships),
				Time.GetTicksMsec() / 1000.0);
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
			PostBark(_barkConfig?.Get("player_under_attack"));
			PostAiBark(aiPlayer);
		}

		_commitmentController.StartCommitment(
			toIndex,
			senderOwner,
			intent,
			BuildFleetInfluences(fleet),
			Time.GetTicksMsec() / 1000.0,
			target.Ships);
	}

	private void CommitAiOwnSystem(int systemIndex, AiPlayerData player, IntentType intent)
	{
		_commitmentController.StartCommitment(
			systemIndex,
			player.Owner,
			intent,
			BuildFleetInfluences(_systems[systemIndex].Ships),
			Time.GetTicksMsec() / 1000.0);
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

	// Maps raw ship count to FleetInfluences for any commitment intent.
	// All four values scale with ships so that fleet size affects every intent type.
	// Ratios reflect what a transit fleet is likely good at: strong on aggression/discipline,
	// weaker on curiosity/stability — those scale at lower rates but are not fixed.
	// The Aggression multiplier must match CommitmentController.InfluenceAggressionPerShip.
	private static FleetInfluences BuildFleetInfluences(float ships) => new(
		Aggression: ships * CommitmentController.InfluenceAggressionPerShip,
		Discipline: ships * 0.08f,
		Curiosity:  ships * 0.05f,
		Stability:  ships * 0.04f);

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
