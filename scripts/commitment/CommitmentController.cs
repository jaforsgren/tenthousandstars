using System;
using System.Collections.Generic;
using Godot;
using Tts.Config;
using Tts.Level;
using Tts.Nodes;

namespace Tts.Commitment;

// Godot Node that drives CommitmentEngine each _Process tick.
// Owns all active CommitmentState objects, per-system SystemHiddenState,
// and the CommitmentIndicatorNode visuals that live on each SystemNode.
public partial class CommitmentController : Node
{
    [Signal]
    public delegate void CommitmentResolvedEventHandler(int systemIndex, int ownerInt, int intentInt, bool controlGained, float remainingStrength);

    [Signal]
    public delegate void CommitmentSignalChangedEventHandler(int systemIndex);

    private const string IndicatorScenePath = ScenePaths.CommitmentIndicatorNode;

    private readonly List<CommitmentState> _active = [];
    private readonly List<CommitmentState> _pendingResolution = [];
    private readonly List<CommitmentState> _cohabitantBuffer = [];
    private readonly Dictionary<int, SystemHiddenState> _hiddenStates = new();
    private readonly Dictionary<Guid, CommitmentIndicatorNode> _indicators = new();

    private CommitmentConfig _config = null!;
    private Random _rng = null!;
    private IReadOnlyList<SystemNode> _systems = null!;
    private Func<SystemOwner, Color> _ownerColor = null!;
    private float _systemRadius;
    private PackedScene _indicatorScene = null!;

    public void Initialize(
        CommitmentConfig config,
        IReadOnlyList<SystemNode> systems,
        Func<SystemOwner, Color> ownerColor,
        float systemRadius,
        int seed)
    {
        _config = config;
        _systems = systems;
        _ownerColor = ownerColor;
        _systemRadius = systemRadius;
        _rng = new Random(seed);
        _indicatorScene = GD.Load<PackedScene>(IndicatorScenePath);

        for (var i = 0; i < systems.Count; i++)
            _hiddenStates[i] = GenerateHiddenState();
    }

    public CommitmentState StartCommitment(
        int systemIndex,
        SystemOwner owner,
        IntentType intent,
        FleetInfluences influences,
        double currentTime)
    {
        var existing = _active.Find(c =>
            c.SystemIndex == systemIndex &&
            c.Owner == owner &&
            c.Intent == intent &&
            c.IsActive);

        if (existing != null)
        {
            existing.Influences = new FleetInfluences(
                existing.Influences.Aggression + influences.Aggression,
                existing.Influences.Discipline + influences.Discipline,
                existing.Influences.Curiosity  + influences.Curiosity,
                existing.Influences.Stability  + influences.Stability);
            existing.InitialFleetStrength += influences.Aggression / _config.FleetInfluencesPerShip.Aggression;
            EmitSignal(SignalName.CommitmentSignalChanged, systemIndex);
            return existing;
        }

        var commitment = new CommitmentState
        {
            SystemIndex               = systemIndex,
            Owner                     = owner,
            Intent                    = intent,
            Influences                = influences,
            InitialFleetStrength      = influences.Aggression / _config.FleetInfluencesPerShip.Aggression,
            StartTime                 = currentTime
        };
        _active.Add(commitment);
        SpawnIndicator(commitment);
        EmitSignal(SignalName.CommitmentSignalChanged, systemIndex);
        return commitment;
    }

    public (InterruptPenalty Penalty, CommitmentOutcome PartialOutcome)? InterruptCommitment(Guid id)
    {
        var commitment = _active.Find(c => c.Id == id);
        if (commitment == null) return null;

        var result = CommitmentEngine.Interrupt(commitment, _config);
        RemoveIndicator(id);
        EmitSignal(SignalName.CommitmentSignalChanged, commitment.SystemIndex);
        return result;
    }

    public IReadOnlyList<CommitmentState> GetActiveForSystem(int systemIndex)
    {
        _cohabitantBuffer.Clear();
        foreach (var c in _active)
            if (c.SystemIndex == systemIndex && c.IsActive)
                _cohabitantBuffer.Add(c);
        return _cohabitantBuffer;
    }

    public IReadOnlyList<CommitmentState> GetAllActive() => _active;

    public SystemVisibleState GetVisibleState(int systemIndex)
    {
        var cohabitants = GetActiveForSystem(systemIndex);
        var hidden = _hiddenStates.GetValueOrDefault(systemIndex);
        return CommitmentEngine.ComputeVisibleState(cohabitants, hidden);
    }

    public override void _Process(double delta)
    {
        foreach (var commitment in _active)
        {
            if (!commitment.IsActive) continue;

            var hidden = _hiddenStates[commitment.SystemIndex];
            var cohabitants = GetActiveForSystem(commitment.SystemIndex);
            CommitmentEngine.Tick(commitment, ref hidden, cohabitants, _config, _rng, (float)delta);
            _hiddenStates[commitment.SystemIndex] = hidden;

            if (_indicators.TryGetValue(commitment.Id, out var indicator))
                indicator.UpdateState(commitment.Phase, commitment.PhaseProgress, commitment.Intent);

            if (commitment.IsComplete)
                _pendingResolution.Add(commitment);
        }

        foreach (var commitment in _pendingResolution)
        {
            RemoveIndicator(commitment.Id);
            var hidden = _hiddenStates[commitment.SystemIndex];
            var defenderFleet = _systems[commitment.SystemIndex].Ships;
            var outcome = CommitmentEngine.Resolve(commitment, hidden, _config, _rng, defenderFleet);
            ApplySystemChanges(commitment.SystemIndex, outcome.SystemChanges);
            var remaining = Math.Max(0f, commitment.InitialFleetStrength + outcome.FleetChanges.StrengthDelta);
            EmitSignal(SignalName.CommitmentResolved,
                commitment.SystemIndex,
                (int)commitment.Owner,
                (int)commitment.Intent,
                outcome.ControlChange != ControlChange.None,
                remaining);
            EmitSignal(SignalName.CommitmentSignalChanged, commitment.SystemIndex);
        }
        _pendingResolution.Clear();

        _active.RemoveAll(c => !c.IsActive);
    }

    private void SpawnIndicator(CommitmentState commitment)
    {
        var slot = CountActiveIndicatorsAt(commitment.SystemIndex);
        var color = _ownerColor(commitment.Owner);
        var indicator = _indicatorScene.Instantiate<CommitmentIndicatorNode>();
        _systems[commitment.SystemIndex].AddChild(indicator);
        indicator.Initialize(_systemRadius, color, slot);
        indicator.UpdateState(commitment.Phase, commitment.PhaseProgress, commitment.Intent);
        _indicators[commitment.Id] = indicator;
    }

    private void RemoveIndicator(Guid id)
    {
        if (!_indicators.TryGetValue(id, out var indicator)) return;
        indicator.QueueFree();
        _indicators.Remove(id);
    }

    private int CountActiveIndicatorsAt(int systemIndex)
    {
        var count = 0;
        foreach (var c in _active)
            if (c.SystemIndex == systemIndex && c.IsActive)
                count++;
        return count;
    }

    private void ApplySystemChanges(int systemIndex, SystemChanges changes)
    {
        if (!_hiddenStates.TryGetValue(systemIndex, out var state)) return;
        state.Instability = Math.Clamp(state.Instability + changes.InstabilityDelta, 0f, 1f);
        state.Corruption  = Math.Clamp(state.Corruption  + changes.CorruptionDelta,  0f, 1f);
        _hiddenStates[systemIndex] = state;
    }

    private SystemHiddenState GenerateHiddenState()
    {
        var r = _config.HiddenStateInitRange;
        return new SystemHiddenState(
            Instability:     (float)_rng.NextDouble() * r,
            Hostility:       (float)_rng.NextDouble() * r,
            RelicPresence:   (float)_rng.NextDouble() * r,
            Corruption:      (float)_rng.NextDouble() * r,
            ResistanceLevel: 0.3f + (float)_rng.NextDouble() * r);
    }
}
