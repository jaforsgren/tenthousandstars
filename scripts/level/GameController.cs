using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Tts.Level;

internal sealed partial class GameController : Node
{
	[Signal]
	public delegate void GameEndedEventHandler();

	internal bool IsEnded { get; private set; }
	internal int ObjectiveSystemIndex { get; private set; } = -1;

	private EndCondition? _condition;
	private EndStateConfig _endStateCfg = null!;
	private string? _missionDescription;
	private NarrativePageData? _missionBriefPage;
	private SystemOwner _targetPlayerOwner = SystemOwner.None;
	private int _defendSystemIndex = -1;

	private IReadOnlyList<SystemNode> _systems = null!;
	private LevelUi _levelUi = null!;
	private CameraController _camera = null!;
	private CountdownTimerNode _countdownTimer = null!;
	private Random _rng = null!;
	private IReadOnlyList<AiPlayerData> _aiPlayers = null!;
	private float _fadeOutSeconds;

	internal void Initialize(
		EndCondition? condition,
		EndStateConfig endStateCfg,
		NarrativePageData? missionBriefPage,
		HashSet<(int, int)> routeSet,
		IReadOnlyList<SystemNode> systems,
		IReadOnlyList<AiPlayerData> aiPlayers,
		Random rng,
		LevelUi levelUi,
		CameraController camera,
		CountdownTimerNode countdownTimer,
		float fadeOutSeconds)
	{
		_condition = condition;
		_endStateCfg = endStateCfg;
		_missionBriefPage = missionBriefPage;
		_systems = systems;
		_aiPlayers = aiPlayers;
		_rng = rng;
		_levelUi = levelUi;
		_camera = camera;
		_countdownTimer = countdownTimer;
		_fadeOutSeconds = fadeOutSeconds;

		if (condition?.TargetSystemHops.HasValue == true)
		{
			ObjectiveSystemIndex = FindObjectiveSystemIndex(condition.TargetSystemHops.Value, systems, routeSet);
			if (ObjectiveSystemIndex >= 0)
				systems[ObjectiveSystemIndex].MarkAsObjective();
		}

		if (condition?.EliminateTargetPlayer == true && aiPlayers.Count > 0)
			ResolveTargetPlayer();

		if (condition?.DefendObjectiveSystem == true)
			ResolveDefendSystem();

		if (condition?.TimeoutSeconds.HasValue == true)
		{
			countdownTimer.Show();
			countdownTimer.Initialize(condition.TimeoutSeconds.Value, OnCountdownExpired);
		}
	}

	internal void StartMission()
	{
		if (_missionBriefPage != null)
			SpawnNarrativeScreen(_missionBriefPage);
		else
			ShowMissionBrief();
	}

	internal void EvaluateEndState()
	{
		CheckVictory();
		CheckDefeat();
	}

	private void CheckVictory()
	{
		if (IsEnded || _condition == null) return;
		if (!EndConditionEvaluator.IsVictoryMet(_condition, _systems, ObjectiveSystemIndex, _targetPlayerOwner, _defendSystemIndex))
			return;

		TriggerGameEnd();
		ShowEndSequence("Mission Complete", _condition.EndDescription, won: true);
	}

	private void CheckDefeat()
	{
		if (IsEnded) return;

		if (EndConditionEvaluator.IsDefeatByAbandonedDefend(_condition, _systems, _defendSystemIndex))
		{
			TriggerGameEnd();
			ShowEndSequence("Defeated", "The marked system has fallen. The mission is lost.", won: false);
			return;
		}

		if (!EndConditionEvaluator.IsDefeatByElimination(_systems)) return;

		TriggerGameEnd();
		var description = _endStateCfg.DefeatDescriptions[_rng.Next(_endStateCfg.DefeatDescriptions.Length)];
		ShowEndSequence("Defeated", description, won: false);
	}

	private void TriggerGameEnd()
	{
		IsEnded = true;
		_levelUi.HideForGameEnd();
		EmitSignal(SignalName.GameEnded);
	}

	private void ShowMissionBrief()
	{
		GameSpeed.PushUiPause();
		_levelUi.NotificationPanel.Show(
			"Mission",
			_missionDescription ?? _condition!.Description,
			_endStateCfg.MissionBriefSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: GameSpeed.PopUiPause);
	}

	private void ShowEndSequence(string title, string description, bool won)
	{
		GameSession.NarrativeController?.OnMissionComplete(
			won,
			_systems.Count(s => s.IsPlayerOwned),
			_systems.Count(s => s.IsAiOwned),
			_systems.Count(s => s.IsAiOwned && s.HasFleet));
		_levelUi.FadeOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
		_camera.PanTo(ComputeMapCenter(), _endStateCfg.EndStateSeconds);
		StartFadeOut(_fadeOutSeconds);
		_levelUi.NotificationPanel.Show(
			title,
			description,
			_endStateCfg.EndStateSeconds,
			GetViewport().GetVisibleRect().Size,
			onDismiss: OnEndSequenceDismissed,
			allowEarlyDismiss: false);
	}

	private void OnEndSequenceDismissed()
	{
		if (GameSession.NarrativeController?.IsCampaignComplete == true)
			SpawnOutroPanel();
		else
			GetTree().ReloadCurrentScene();
	}

	private void SpawnOutroPanel()
	{
		var storyText = GameSession.NarrativeController!.GenerateOutro();
		var state = GameSession.NarrativeController!.CurrentState;
		var data = NarrativePageData.FromOutro(storyText, state);

		var layer = new CanvasLayer { Layer = 14 };
		AddChild(layer);
		var screen = GD.Load<PackedScene>("res://scenes/ui/NarrativeScreen.tscn").Instantiate<NarrativeScreen>();
		layer.AddChild(screen);
		screen.ShowOutro(data);
		screen.NewCampaignPressed += OnNewCampaignPressed;
		screen.RandomMissionsPressed += OnRandomMissionsPressed;
		screen.QuitPressed += () => GetTree().Quit();
	}

	private void SpawnNarrativeScreen(NarrativePageData data)
	{
		var layer = new CanvasLayer { Layer = 13 };
		AddChild(layer);
		var screen = GD.Load<PackedScene>("res://scenes/ui/NarrativeScreen.tscn").Instantiate<NarrativeScreen>();
		layer.AddChild(screen);
		screen.ShowMissionBrief(data, onStartMission: () => layer.QueueFree());
	}

	private void OnNewCampaignPressed()
	{
		GameSession.NarrativeController = null;
		GameSession.GameModeOverride = GameMode.Story;
		GetTree().ReloadCurrentScene();
	}

	private void OnRandomMissionsPressed()
	{
		GameSession.NarrativeController = null;
		GameSession.GameModeOverride = GameMode.Random;
		GetTree().ReloadCurrentScene();
	}

	private void OnCountdownExpired()
	{
		if (IsEnded) return;
		TriggerGameEnd();
		ShowEndSequence("Time Expired", _condition?.TimeoutMessage ?? "The mission clock has run out.", won: false);
	}

	private void StartFadeOut(float durationSeconds)
	{
		var tween = CreateTween();
		tween.TweenProperty(_levelUi.FadeOverlay, "modulate", new Color(1f, 1f, 1f, 1f), durationSeconds)
			.SetTrans(Tween.TransitionType.Linear);
	}

	private Vector2 ComputeMapCenter()
	{
		var sum = Vector2.Zero;
		foreach (var system in _systems)
			sum += system.GlobalPosition;
		return sum / _systems.Count;
	}

	private void ResolveTargetPlayer()
	{
		var target = _aiPlayers[_rng.Next(_aiPlayers.Count)];
		_targetPlayerOwner = target.Owner;
		foreach (var system in _systems)
			system.SetTargetOwner(_targetPlayerOwner);
		_missionDescription = $"{target.FactionName} marked for elimination. Destroy them before time runs out.";
	}

	private void ResolveDefendSystem()
	{
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned) continue;
			_defendSystemIndex = i;
			_systems[i].MarkAsDefend();
			return;
		}
	}

	private static int FindObjectiveSystemIndex(int targetHops, IReadOnlyList<SystemNode> systems, HashSet<(int, int)> routeSet)
	{
		var playerIndex = -1;
		for (var i = 0; i < systems.Count; i++)
		{
			if (!systems[i].IsPlayerOwned) continue;
			playerIndex = i;
			break;
		}
		if (playerIndex < 0) return -1;

		var distances = GraphUtils.BfsHopDistances(playerIndex, systems.Count, routeSet);

		var maxHops = 0;
		var maxIndex = -1;
		for (var i = 0; i < distances.Length; i++)
		{
			if (distances[i] <= maxHops) continue;
			maxHops = distances[i];
			maxIndex = i;
		}

		if (targetHops >= maxHops)
			return maxIndex;

		var bestIndex = -1;
		var bestDelta = int.MaxValue;
		for (var i = 0; i < distances.Length; i++)
		{
			if (i == playerIndex) continue;
			var delta = Math.Abs(distances[i] - targetHops);
			if (delta < bestDelta)
			{
				bestDelta = delta;
				bestIndex = i;
			}
		}
		return bestIndex;
	}
}
