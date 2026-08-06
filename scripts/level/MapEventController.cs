using System;
using System.Collections.Generic;
using Godot;
using Tts.Config;

namespace Tts.Level;

// Fires authored map events (config/maps/*.json) as Yarn dialogues. Events are
// triggered by player actions — attacking a system, conquering it, or a fixed
// delay after either — plus a mission-start delay. Delayed events tick on scaled
// game time so they pause while a dialogue or other UI overlay is showing.
//
// Dialogues are queued on the NarrativePanel, so an event firing mid-commitment
// dialogue waits for it to finish instead of interrupting the runner.
public partial class MapEventController : Node
{
	private sealed class PendingEvent
	{
		public required int EventIndex;
		public float RemainingSeconds;
	}

	private readonly List<MapEvent> _events = [];
	private readonly Dictionary<int, int> _firesLeft = new();
	private readonly List<PendingEvent> _pending = [];
	private NarrativePanel _narrativePanel = null!;
	private IReadOnlyDictionary<string, string> _vars = new Dictionary<string, string>();

	public void Initialize(
		IReadOnlyList<MapEvent> events,
		NarrativePanel narrativePanel,
		IReadOnlyDictionary<string, string> vars)
	{
		_events.Clear();
		_events.AddRange(events);
		_narrativePanel = narrativePanel;
		_vars = vars;
		_firesLeft.Clear();
		for (var i = 0; i < _events.Count; i++)
			_firesLeft[i] = _events[i].MaxFires;
	}

	public override void _Process(double delta)
	{
		for (var i = _pending.Count - 1; i >= 0; i--)
		{
			var pending = _pending[i];
			pending.RemainingSeconds -= (float)delta;
			if (pending.RemainingSeconds > 0f) continue;

			_pending.RemoveAt(i);
			Fire(pending.EventIndex);
		}
	}

	public void NotifyMissionStart() => ScheduleDelayed(MapEventTrigger.MissionStart);

	public void NotifyPlayerAttack() => Notify(RequestKind.Attack);

	public void NotifyPlayerConquer() => Notify(RequestKind.Conquer);

	private enum RequestKind { Attack, Conquer }

	private void Notify(RequestKind kind)
	{
		for (var i = 0; i < _events.Count; i++)
		{
			if (_events[i].Trigger != (kind == RequestKind.Attack ? MapEventTrigger.Attack : MapEventTrigger.Conquer))
				continue;
			FireImmediate(i);
		}
		ScheduleDelayed(kind == RequestKind.Attack ? MapEventTrigger.AttackDelayed : MapEventTrigger.ConquerDelayed);
	}

	private void FireImmediate(int eventIndex)
	{
		if (_firesLeft[eventIndex] <= 0) return;
		ConsumeAndShow(eventIndex);
	}

	private void ScheduleDelayed(MapEventTrigger trigger)
	{
		for (var i = 0; i < _events.Count; i++)
		{
			if (_events[i].Trigger != trigger) continue;
			if (_firesLeft[i] <= 0) continue;
			_pending.Add(new PendingEvent { EventIndex = i, RemainingSeconds = _events[i].DelaySeconds });
		}
	}

	private void Fire(int eventIndex)
	{
		if (_firesLeft[eventIndex] <= 0) return;
		ConsumeAndShow(eventIndex);
	}

	private void ConsumeAndShow(int eventIndex)
	{
		_firesLeft[eventIndex] = Math.Max(0, _firesLeft[eventIndex] - 1);
		_narrativePanel.ShowNarrative(_events[eventIndex].YarnNode, _vars);
	}
}