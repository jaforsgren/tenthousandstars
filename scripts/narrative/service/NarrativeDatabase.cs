using System;
using System.Collections.Generic;
using Tts.Config;

namespace Tts.Narrative;

public class NarrativeDatabase
{
	public IReadOnlyList<ArchetypeConfig> Archetypes { get; }
	public IReadOnlyList<ChapterDefConfig> Chapters { get; }
	public IReadOnlyList<NarrativeConditionConfig> Conditions { get; }
	public IReadOnlyDictionary<string, string[]> BriefingPools { get; }
	public IReadOnlyDictionary<string, string[]> NarrativeBarkPools { get; }
	public ScenarioConfig Scenarios { get; }

	public NarrativeDatabase(
		IReadOnlyList<ArchetypeConfig> archetypes,
		IReadOnlyList<ChapterDefConfig> chapters,
		IReadOnlyList<NarrativeConditionConfig> conditions,
		IReadOnlyDictionary<string, string[]> briefingPools,
		IReadOnlyDictionary<string, string[]> narrativeBarkPools,
		ScenarioConfig scenarios)
	{
		Archetypes = archetypes;
		Chapters = chapters;
		Conditions = conditions;
		BriefingPools = briefingPools;
		NarrativeBarkPools = narrativeBarkPools;
		Scenarios = scenarios;
	}

	public ArchetypeConfig GetArchetype(string id)
	{
		foreach (var archetype in Archetypes)
			if (archetype.Id == id) return archetype;
		throw new InvalidOperationException($"Archetype not found: '{id}'");
	}

	public ChapterDefConfig GetChapter(string id)
	{
		foreach (var chapter in Chapters)
			if (chapter.Id == id) return chapter;
		throw new InvalidOperationException($"Chapter definition not found: '{id}'");
	}
}
