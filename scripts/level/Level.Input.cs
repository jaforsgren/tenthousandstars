using System.Collections.Generic;
using Godot;
using Tts.Commitment;
using Tts.Types;
using Tts.Ui;
using Tts.Utils;

namespace Tts.Level;

public partial class Level
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || _endConditionReached || GameSpeed.IsUiPaused)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } key)
			HandleKeyDebug(key);
		else if (@event is InputEventMouseButton mb)
			HandleMouseButton(mb);
		else if (@event is InputEventMouseMotion)
			HandleMouseMotion();
	}

	private void HandleKeyDebug(InputEventKey key)
	{
		if (!OS.IsDebugBuild()) return;
		switch (key.Keycode)
		{
			case Key.F1:
				DebugOverlay.ShowHelp();
				GetViewport().SetInputAsHandled();
				break;
			case Key.F2:
				var idx = _systems.FindIndex(s => s.IsPlayerOwned);
				if (idx >= 0) SelectSystem(idx);
				GetViewport().SetInputAsHandled();
				break;
			case Key.F3:
				ToggleDebugFog();
				GetViewport().SetInputAsHandled();
				break;
			case Key.F4:
				GetTree().ChangeSceneToFile("res://scenes/debug/StoryDebugScene.tscn");
				GetViewport().SetInputAsHandled();
				break;
			case Key.F5:
				DebugOverlay.LogAi(_aiController.GetDebugState());
				GetViewport().SetInputAsHandled();
				break;
			case Key.F6:
				GetTree().ChangeSceneToFile("res://scenes/debug/NarrativeDebugRoot.tscn");
				GetViewport().SetInputAsHandled();
				break;
		}
	}

	private void HandleMouseButton(InputEventMouseButton e)
	{
		if (e.ButtonIndex != MouseButton.Left)
			return;

		if (e.Pressed)
		{
			_pressWorldPos = GetGlobalMousePosition();
			TryRegisterDragCandidate();
		}
		else if (_drag.IsActive)
		{
			EndFleetDrag();
			_drag.HasCandidate = false;
			_drag.CandidateIndex = -1;
		}
		else
		{
			HandleClick(_pressWorldPos);
			_drag.HasCandidate = false;
			_drag.CandidateIndex = -1;
		}
	}

	private void HandleMouseMotion()
	{
		if (_drag.HasCandidate && !_drag.IsActive)
		{
			var worldPos = GetGlobalMousePosition();
			if (worldPos.DistanceTo(_pressWorldPos) > DragThreshold)
			{
				_drag.IsActive = true;
				_drag.FromIndex = _drag.CandidateIndex;
				_drag.WorldPos = worldPos;
				if (_drag.IsCapitolShip)
					_systems[_drag.FromIndex].SetCapitolShipVisible(false);
				else
					_systems[_drag.FromIndex].RefreshFleetVisuals();
				_levelUi.SystemActionMenu.HideAll();
				QueueRedraw();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (_drag.IsActive)
		{
			_drag.WorldPos = GetGlobalMousePosition();
			UpdateDragPath(_drag.WorldPos);
			QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	private void UpdateDragPath(Vector2 worldPos)
	{
		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == _drag.FromIndex) continue;
			if (_systems[i].FogState == FogState.Hidden) continue;
			if (!_systems[i].ContainsSystemAt(worldPos)) continue;

			var path = GraphUtils.FindPath(
				_drag.FromIndex, i, _adjacency,
				idx => _systems[idx].IsPlayerOwned);
			SetPathHighlight(path);
			return;
		}
		SetPathHighlight(null);
	}

	private void TryRegisterDragCandidate()
	{
		// Capitol ships take priority over regular fleet
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned || !_systems[i].HasCapitolShip) continue;
			if (!_systems[i].ContainsCapitolShipAt(_pressWorldPos)) continue;

			_drag.CandidateIndex = i;
			_drag.FleetSlot = -1;
			_drag.IsCapitolShip = true;
			_drag.HasCandidate = true;
			GetViewport().SetInputAsHandled();
			return;
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned || !_systems[i].HasFleet) continue;
			var slot = _systems[i].GetFleetSlotAt(_pressWorldPos);
			if (slot < 0) continue;

			_drag.CandidateIndex = i;
			_drag.FleetSlot = slot;
			_drag.IsCapitolShip = false;
			_drag.HasCandidate = true;
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	private void HandleClick(Vector2 worldPos)
	{
		if (_isPickingRerouteTarget)
		{
			HandleRerouteTargetPick(worldPos);
			return;
		}

		for (var i = 0; i < _systems.Count; i++)
		{
			if (_systems[i].FogState == FogState.Hidden) continue;

			var fleetSlot = _systems[i].GetFleetSlotAt(worldPos);
			if (_systems[i].HasFleet && fleetSlot >= 0)
			{
				_selectedFleetSlot = fleetSlot;
				SelectFleet(i);
				return;
			}

			if (_systems[i].ContainsSystemAt(worldPos))
			{
				HandleSystemClick(i);
				return;
			}
		}

		_levelUi.AiSystemPanel.Hide();
		_levelUi.SelectionPanel.Hide();
		_camera.ExitFollowMode();
		_levelUi.HideContextMenus();
	}

	private void HandleSystemClick(int systemIndex)
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var isDoubleClick = _lastClickedSystemIndex == systemIndex
			&& (now - _lastSystemClickTime) < DoubleClickThresholdSeconds;
		_lastSystemClickTime = now;
		_lastClickedSystemIndex = systemIndex;

		if (isDoubleClick)
			_camera.FollowSystem(_systems[systemIndex].GlobalPosition);
		else if (_systems[systemIndex].IsAiOwned)
			SelectAiSystem(systemIndex);
		else
			SelectSystem(systemIndex);
	}

	private void OnRerouteButtonPressed(int systemIndex)
	{
		if (_rerouteTargets.ContainsKey(systemIndex))
		{
			ClearReroute(systemIndex);
			_levelUi.SystemActionMenu.RefreshRerouteButton(hasActiveRoute: false, () => OnRerouteButtonPressed(systemIndex));
		}
		else
		{
			_isPickingRerouteTarget = true;
			_rerouteSourceIndex = systemIndex;
			_levelUi.HideContextMenus();
			_levelUi.SelectionPanel.Hide();
		}
	}

	private void HandleRerouteTargetPick(Vector2 worldPos)
	{
		_isPickingRerouteTarget = false;

		if (_rerouteSourceIndex is not { } sourceIndex)
			return;

		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == sourceIndex) continue;
			if (_systems[i].FogState == FogState.Hidden) continue;
			if (!AreConnected(sourceIndex, i)) continue;
			if (!_systems[i].ContainsSystemAt(worldPos)) continue;

			SetRerouteTarget(sourceIndex, i);
			SelectSystem(sourceIndex);
			_rerouteSourceIndex = null;
			return;
		}

		_rerouteSourceIndex = null;
	}

	private void EndFleetDrag()
	{
		SetPathHighlight(null);
		var worldPos = GetGlobalMousePosition();
		var resolved = false;

		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == _drag.FromIndex) continue;
			if (!_systems[i].ContainsSystemAt(worldPos)) continue;
			if (_systems[i].FogState == FogState.Hidden) continue;

			var path = GraphUtils.FindPath(
				_drag.FromIndex, i, _adjacency,
				idx => _systems[idx].IsPlayerOwned);
			if (path == null) break;

			if (_drag.IsCapitolShip)
				resolved = TryLaunchCapitolShip(_drag.FromIndex, i, path);
			else
				resolved = TryLaunchPathedFleet(_drag.FromIndex, _drag.FleetSlot, path);
			break;
		}

		if (!resolved)
		{
			if (_drag.IsCapitolShip)
				_systems[_drag.FromIndex].SetCapitolShipVisible(true);
			else
				_systems[_drag.FromIndex].RefreshFleetVisuals();
		}

		_drag = DragState.None;
		QueueRedraw();

		if (resolved)
			UpdateFog();
	}

	private bool TryLaunchPathedFleet(int fromIndex, int fleetSlot, List<int> path)
	{
		var fleet = _systems[fromIndex].TakeFleet(fleetSlot);
		PostPlayerTransitBark(path[^1]);
		LaunchPlayerPathedTransit(path, fleet);
		return true;
	}

	private bool TryLaunchCapitolShip(int fromIndex, int toIndex, List<int> path)
	{
		var target = _systems[toIndex];
		(string Label, IntentType Intent)[] intents = target.IsPlayerOwned
			? [("Fortify", IntentType.Fortify), ("Investigate", IntentType.Investigate), ("Exploit", IntentType.Exploit)]
			: [("Attack", IntentType.Attack), ("Contest", IntentType.Contest)];

		_systems[fromIndex].TakeCapitolShip();

		_levelUi.SystemActionMenu.ShowIntentOnly(
			target.GlobalPosition,
			intents,
			intent =>
			{
				LaunchCapitolPathedTransit(path, intent);
				UpdateFog();
			},
			required: true);

		return true;
	}
}
