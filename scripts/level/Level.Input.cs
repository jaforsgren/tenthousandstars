using Godot;
using Tts.Types;
using Tts.Ui;

namespace Tts.Level;

public partial class Level
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || _endConditionReached)
			return;

		if (@event is InputEventMouseButton mb)
			HandleMouseButton(mb);
		else if (@event is InputEventMouseMotion)
			HandleMouseMotion();
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
				_systems[_drag.FromIndex].RefreshFleetVisuals();
				_levelUi.SystemActionMenu.HideAll();
				QueueRedraw();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (_drag.IsActive)
		{
			_drag.WorldPos = GetGlobalMousePosition();
			QueueRedraw();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TryRegisterDragCandidate()
	{
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned || !_systems[i].HasFleet)
				continue;
			var slot = _systems[i].GetFleetSlotAt(_pressWorldPos);
			if (slot < 0) continue;

			_drag.CandidateIndex = i;
			_drag.FleetSlot = slot;
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
		var worldPos = GetGlobalMousePosition();
		var resolved = false;

		for (var i = 0; i < _systems.Count; i++)
		{
			if (i == _drag.FromIndex || !_systems[i].ContainsSystemAt(worldPos))
				continue;
			if (!AreConnected(_drag.FromIndex, i))
				continue;

			var fromIndex = _drag.FromIndex;
			var toIndex = i;
			var fleet = _systems[fromIndex].TakeFleet(_drag.FleetSlot);

			PostPlayerTransitBark(toIndex);
			LaunchPlayerTransit(fromIndex, toIndex, fleet);

			resolved = true;
			break;
		}

		if (!resolved)
			_systems[_drag.FromIndex].RefreshFleetVisuals();

		_drag = DragState.None;
		QueueRedraw();

		if (resolved)
			UpdateFog();
	}
}
