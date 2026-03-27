using System;
using System.Collections.Generic;

namespace Tts;

public class NarrativeDatabase
{
	public IReadOnlyList<ArchetypeConfig> Archetypes { get; }
	public IReadOnlyList<ChapterDefConfig> Chapters { get; }
	public IReadOnlyList<NarrativeConditionConfig> Conditions { get; }
	public BriefingConfig BriefingTemplates { get; }
	public NarrativeBarkConfig Barks { get; }
	public InterludeConfig Interludes { get; }
	public OutroConfig Outro { get; }

	public NarrativeDatabase(
		IReadOnlyList<ArchetypeConfig> archetypes,
		IReadOnlyList<ChapterDefConfig> chapters,
		IReadOnlyList<NarrativeConditionConfig> conditions,
		BriefingConfig briefingTemplates,
		NarrativeBarkConfig barks,
		InterludeConfig interludes,
		OutroConfig outro)
	{
		Archetypes = archetypes;
		Chapters = chapters;
		Conditions = conditions;
		BriefingTemplates = briefingTemplates;
		Barks = barks;
		Interludes = interludes;
		Outro = outro;
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
