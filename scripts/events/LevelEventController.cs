using System;
using System.Collections.Generic;

namespace Tts.Events;

// Owns the scenario assigned to a level, the event pool for that scenario,
// chain queuing, and the live encounter chance (modifiable by events).
public sealed class LevelEventController
{
    public static LevelEventController? Instance { get; private set; }

    private readonly LevelScenarioType _scenario;
    private readonly Random _rng;
    private float _encounterChance;
    private readonly HashSet<string> _seenNodes = [];
    private string? _pendingChainNode;

    public LevelScenarioType Scenario => _scenario;
    public float EncounterChance => _encounterChance;

    public LevelEventController(LevelScenarioType scenario, float baseEncounterChance, Random rng)
    {
        _scenario = scenario;
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
        var pool = EventPool(_scenario);
        var unseen = pool.FindAll(n => !_seenNodes.Contains(n));
        var candidates = unseen.Count > 0 ? unseen : pool;
        if (candidates.Count == 0) return null;
        return candidates[_rng.Next(candidates.Count)];
    }

    private static List<string> EventPool(LevelScenarioType scenario) => scenario switch
    {
        LevelScenarioType.BarbarianHorde => ["barbarian_encounter", "barbarian_ritual", "barbarian_relic"],
        LevelScenarioType.AiUprising    => ["ai_awakening",         "ai_negotiation",  "ai_sabotage"],
        LevelScenarioType.Nemesis1      => ["nemesis1_contact",     "nemesis1_ambush", "nemesis1_messenger"],
        LevelScenarioType.Nemesis2      => ["nemesis2_defense",     "nemesis2_sanctum","nemesis2_price"],
        _                               => []
    };
}
