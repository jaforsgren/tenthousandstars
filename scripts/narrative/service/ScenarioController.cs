using System;
using System.Collections.Generic;
using System.Linq;

namespace Tts;

public class ScenarioController
{
    private readonly Dictionary<int, ScenarioDefinition> _systemScenarios = new();

    public bool HasScenario(int systemIndex) => _systemScenarios.ContainsKey(systemIndex);

    public ScenarioDefinition? GetScenario(int systemIndex)
        => _systemScenarios.TryGetValue(systemIndex, out var s) ? s : null;

    public void DismissScenario(int systemIndex) => _systemScenarios.Remove(systemIndex);

    public void AssignScenarios(
        IReadOnlyList<SystemNode> systems,
        ScenarioDefinition[] eligibleScenarios,
        Random rng)
    {
        _systemScenarios.Clear();

        if (eligibleScenarios.Length == 0) return;

        var candidateIndices = Enumerable.Range(0, systems.Count)
            .Where(i => !systems[i].IsPlayerOwned)
            .OrderBy(_ => rng.Next())
            .Take(eligibleScenarios.Length)
            .ToList();

        for (var i = 0; i < candidateIndices.Count && i < eligibleScenarios.Length; i++)
            _systemScenarios[candidateIndices[i]] = eligibleScenarios[i];
    }
}
