using Godot;

namespace Tts;

public partial class Level
{
	private void ResolvePlayerTransitArrival(int toIndex, float fleet)
	{
		var target = _systems[toIndex];

		if (target.Owner == SystemOwner.Player)
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
		if (_systems[toIndex].Owner == SystemOwner.Player)
		{
			PostBark(_barkConfig?.PlayerUnderAttack);
			PostAiBark(aiPlayer);
		}

		var dotColor = _aiColors[aiPlayer.Owner];
		var fromEdge = EdgeToward(_systems[fromIndex].Position, _systems[toIndex].Position, _systemRadius);
		var toEdge = EdgeToward(_systems[toIndex].Position, _systems[fromIndex].Position, _systemRadius);

		var transit = _transitFleetScene.Instantiate<TransitFleetNode>();
		AddChild(transit);
		transit.Launch(fromEdge, toEdge, dotColor, _transitDurationSeconds,
			() => ResolveAiTransitArrival(toIndex, fleet, aiPlayer.Owner, aiPlayer, dotColor));
	}

	private void ResolveAiTransitArrival(int toIndex, float fleet, SystemOwner senderOwner, AiPlayerData aiPlayer, Color aiOwnerColor)
	{
		var target = _systems[toIndex];

		if (target.Owner == senderOwner)
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

	private void SpawnCombatEffect(int systemIndex, bool attackerWon)
	{
		var pos = _systems[systemIndex].Position;

		var impact = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(impact);
		impact.Position = pos;
		impact.PlayImpact();

		if (!attackerWon)
			return;

		var capture = _combatEffectScene.Instantiate<CombatEffectNode>();
		AddChild(capture);
		capture.Position = pos;
		capture.PlayCapture();
	}

	private void PostPlayerTransitBark(int toIndex)
	{
		var pool = _systems[toIndex].Owner == SystemOwner.Player
			? _barkConfig?.PlayerMove
			: _barkConfig?.PlayerAttack;
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
		if (_chatWindow == null)
			return;
		if (!_aiDispositionBarks.TryGetValue(aiPlayer.Disposition.ToString(), out var messages) || messages.Length == 0)
			return;
		var message = messages[_rng.Next(messages.Length)];
		_chatWindow.PostMessage(aiPlayer.FactionName, message);
	}
}
