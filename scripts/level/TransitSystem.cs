using System.Collections.Generic;
using Tts.Fleet;

namespace Tts.Level;

internal sealed class TransitSystem
{
	private readonly List<ActiveTransit> _activeTransits = [];

	// Returns the opposing transit if a route combat should be scheduled, null otherwise.
	internal ActiveTransit? Add(ActiveTransit transit)
	{
		var opponent = _activeTransits.Find(t =>
			t.FromIndex == transit.ToIndex &&
			t.ToIndex == transit.FromIndex &&
			t.Owner != transit.Owner);
		_activeTransits.Add(transit);
		return opponent;
	}

	internal void Remove(ActiveTransit transit) => _activeTransits.Remove(transit);

	internal void RemoveByNodes(TransitFleetNode a, TransitFleetNode b)
		=> _activeTransits.RemoveAll(t => t.Node == a || t.Node == b);

	internal void Clear() => _activeTransits.Clear();
}
