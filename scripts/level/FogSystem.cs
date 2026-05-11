using System;
using System.Collections.Generic;
using Godot;
using Tts.Nodes;
using Tts.Types;

namespace Tts.Level;

internal sealed class FogSystem
{
	private readonly IReadOnlyList<SystemNode> _systems;
	private readonly IReadOnlyList<(int From, int To, RouteNode Node)> _routeNodes;
	private readonly Dictionary<int, List<int>> _adjacency;
	private readonly Dictionary<SystemOwner, Color> _aiColors;
	private readonly bool _fogEnabled;
	private readonly float _fogClearSeconds;

	internal FogSystem(
		IReadOnlyList<SystemNode> systems,
		IReadOnlyList<(int From, int To, RouteNode Node)> routeNodes,
		Dictionary<int, List<int>> adjacency,
		Dictionary<SystemOwner, Color> aiColors,
		bool fogEnabled,
		float fogClearSeconds)
	{
		_systems = systems;
		_routeNodes = routeNodes;
		_adjacency = adjacency;
		_aiColors = aiColors;
		_fogEnabled = fogEnabled;
		_fogClearSeconds = fogClearSeconds;
	}

	internal void Update(int objectiveSystemIndex, IReadOnlyCollection<int>? committedByPlayer = null)
	{
		if (!_fogEnabled)
		{
			for (var i = 0; i < _systems.Count; i++)
				_systems[i].SetFogState(FogState.Revealed, _fogClearSeconds);
			foreach (var (from, to, routeNode) in _routeNodes)
			{
				routeNode.SetFogState(FogState.Revealed, _fogClearSeconds);
				routeNode.SetOwnerColor(SharedOwnerColor(from, to));
			}
			return;
		}

		var scoutedByPlayer = new HashSet<int>();
		for (var i = 0; i < _systems.Count; i++)
		{
			if (!_systems[i].IsPlayerOwned) continue;
			foreach (var neighbor in _adjacency[i])
				scoutedByPlayer.Add(neighbor);
		}

		if (committedByPlayer != null)
			foreach (var idx in committedByPlayer)
				scoutedByPlayer.Add(idx);

		var baseStates = new FogState[_systems.Count];

		for (var i = 0; i < _systems.Count; i++)
		{
			FogState baseState;
			if (_systems[i].IsPlayerOwned)
				baseState = FogState.Revealed;
			else if (scoutedByPlayer.Contains(i))
				baseState = FogState.Scouted;
			else
				baseState = FogState.Hidden;

			baseStates[i] = baseState;

			var displayState = (baseState == FogState.Scouted && _systems[i].IsAiOwned)
				? FogState.Revealed
				: baseState;
			var permanent = (FogState)Math.Max((int)displayState, (int)_systems[i].FogState);
			_systems[i].SetFogState(permanent, _fogClearSeconds);
		}

		if (objectiveSystemIndex >= 0 && _systems[objectiveSystemIndex].FogState == FogState.Hidden)
			_systems[objectiveSystemIndex].SetFogState(FogState.Scouted, _fogClearSeconds);

		foreach (var (from, to, routeNode) in _routeNodes)
		{
			var fromState = baseStates[from];
			var toState = baseStates[to];

			FogState routeState;
			if (fromState == FogState.Hidden || toState == FogState.Hidden)
				routeState = FogState.Hidden;
			else if (fromState == FogState.Revealed || toState == FogState.Revealed)
				routeState = FogState.Revealed;
			else
				routeState = FogState.Scouted;

			routeNode.SetFogState(routeState, _fogClearSeconds);
			routeNode.SetOwnerColor(SharedOwnerColor(from, to));
		}
	}

	private Color? SharedOwnerColor(int fromIndex, int toIndex)
	{
		var fromOwner = _systems[fromIndex].OwnerPlayer;
		var toOwner = _systems[toIndex].OwnerPlayer;
		if (fromOwner == SystemOwner.None || fromOwner != toOwner)
			return null;
		return _aiColors.TryGetValue(fromOwner, out var color) ? color : null;
	}
}
