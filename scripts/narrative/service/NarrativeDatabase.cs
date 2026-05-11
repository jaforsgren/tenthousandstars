using System;
using System.Collections.Generic;

namespace Tts.Narrative;

public class NarrativeDatabase
{
	public IReadOnlyList<ArchetypeConfig> Archetypes { get; }
	public IReadOnlyList<ChapterDefConfig> Chapters { get; }
	public IReadOnlyList<NarrativeConditionConfig> Conditions { get; }
	public BriefingConfig BriefingTemplates { get; }
	public BarkConfig Barks { get; }
	public StoryTextConfig StoryTexts { get; }
	public ScenarioConfig Scenarios { get; }

	public NarrativeDatabase(
		IReadOnlyList<ArchetypeConfig> archetypes,
		IReadOnlyList<ChapterDefConfig> chapters,
		IReadOnlyList<NarrativeConditionConfig> conditions,
		BriefingConfig briefingTemplates,
		BarkConfig barks,
		StoryTextConfig storyTexts,
		ScenarioConfig scenarios)
	{
		Archetypes = archetypes;
		Chapters = chapters;
		Conditions = conditions;
		BriefingTemplates = briefingTemplates;
		Barks = barks;
		StoryTexts = storyTexts;
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
