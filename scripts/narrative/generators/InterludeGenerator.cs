using System;
using System.Collections.Generic;

namespace Tts;

public class InterludeGenerator : TagMatchingBase, IInterludeGenerator
{
	private readonly StoryTextConfig _config;
	private readonly Random _rng;
	private readonly HashSet<string> _shownIds = [];
	private string[] _activeTags;

	public InterludeGenerator(StoryTextConfig config, Random rng, string archetypeId)
	{
		_config = config;
		_rng = rng;
		_activeTags = ["begin", archetypeId];
	}

	public StoryText? TryGenerate(MissionContext ctx)
	{
		var candidates = new List<StoryTextTemplate>();
		foreach (var template in _config.Templates)
		{
			if (_shownIds.Contains(template.Id)) continue;
			if (!HasAnyTag(template.Tags, _activeTags)) continue;
			if (!IsInterludeTemplate(template.Tags)) continue;
			candidates.Add(template);
		}

		if (candidates.Count == 0) return null;

		var chosen = candidates[_rng.Next(candidates.Count)];
		_shownIds.Add(chosen.Id);
		_activeTags = chosen.Tags;

		return new StoryText(chosen.Title, ApplyTokens(chosen.Text, ctx));
	}

	private static bool IsInterludeTemplate(string[] tags)
	{
		foreach (var tag in tags)
			if (tag is "begin" or "mid" or "end") return true;
		return false;
	}

	private string ApplyTokens(string text, MissionContext ctx)
	{
		var missionIndex = ctx.State.MissionsCompleted + 1;
		var year = _config.BaseYear + ctx.State.MissionsCompleted;
		var date = _config.DateFormat
			.Replace("{MissionIndex}", missionIndex.ToString())
			.Replace("{Year}", year.ToString());
		var sector = _config.SectorNames[_rng.Next(_config.SectorNames.Length)];

		return text
			.Replace("{PlayerFaction}", ctx.State.Player.FactionName)
			.Replace("{EnemyFaction}", ctx.State.Enemy.FactionName)
			.Replace("{MissionObjective}", ctx.Condition.Description)
			.Replace("{SectorName}", sector)
			.Replace("{ChapterTitle}", ctx.Chapter.ChapterTitle)
			.Replace("{Date}", date);
	}
}
