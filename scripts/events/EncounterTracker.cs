using System;
using System.Collections.Generic;

namespace Tts.Events;

// Tracks the encounter type assigned to a level run, manages the Yarn event pool,
// chains queued events, and maintains the live encounter chance.
public sealed class EncounterTracker
{
    public static EncounterTracker? Instance { get; private set; }

    private readonly EncounterType _encounterType;
    private readonly IReadOnlyDictionary<string, string[]> _eventPools;
    private readonly Random _rng;
    private float _encounterChance;
    private readonly HashSet<string> _seenNodes = [];
    private string? _pendingChainNode;

    public EncounterType EncounterType => _encounterType;
    public float EncounterChance => _encounterChance;

    public EncounterTracker(EncounterType encounterType, float baseEncounterChance, IReadOnlyDictionary<string, string[]> eventPools, Random rng)
    {
        _encounterType = encounterType;
        _eventPools = eventPools;
        _encounterChance = baseEncounterChance;
        _rng = rng;
        Instance = this;
    }

    // Returns the Yarn node name for the next encounter event, or null if the pool is exhausted.
    public string? NextEventNode()
    {
        if (_pendingChainNode != null)
        {
            var chain = _pendingChainNode;
            _pendingChainNode = null;
            return chain;
        }

        return SelectFromPool();
    }

    public void MarkSeen(string node) => _seenNodes.Add(node);

    // Called by <<chain_event "node">> — ensures the next marked system visit triggers this node.
    public void QueueChain(string node) => _pendingChainNode = node;

    // Called by <<modify_encounter_chance delta>> — clamps to [0, 1].
    public void ModifyEncounterChance(float delta)
        => _encounterChance = Math.Clamp(_encounterChance + delta, 0f, 1f);

    private string? SelectFromPool()
    {
        if (!_eventPools.TryGetValue(_encounterType.ToString(), out var pool) || pool.Length == 0)
            return null;
        var unseen = Array.FindAll(pool, n => !_seenNodes.Contains(n));
        var candidates = unseen.Length > 0 ? unseen : pool;
        return candidates[_rng.Next(candidates.Length)];
    }
}
