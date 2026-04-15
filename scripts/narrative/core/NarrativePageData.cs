using System;
using System.Collections.Generic;

namespace Tts;

public record NarrativePageData(
    string ChapterTitle,
    string Faction,
    string? MissionObjective,
    string[] Pages)
{
    private const int LinesPerPage = 10;

    public static NarrativePageData FromMission(MissionContext ctx)
        => new(
            ctx.Chapter.ChapterTitle,
            FormatFaction(ctx.State),
            ctx.Condition.Description,
            Paginate(ctx.Interlude?.Body ?? ctx.Briefing));

    public static NarrativePageData FromOutro(StoryText storyText, StoryState state)
        => new(
            storyText.Title ?? "Campaign Complete",
            FormatFaction(state),
            null,
            Paginate(storyText.Body));

    private static string FormatFaction(StoryState state)
        => $"{ArchetypeName(state.ArchetypeId)} · {state.Player.FactionName}";

    internal static string ArchetypeName(string id) => id switch
    {
        "falling_empire" => "The Falling Empire",
        "rising_power" => "The Rising Power",
        "conquest" => "Total Conquest",
        _ => id
    };

    private static string[] Paginate(string body)
    {
        var lines = body.Split('\n');
        var pages = new List<string>(lines.Length / LinesPerPage + 1);
        for (var i = 0; i < lines.Length; i += LinesPerPage)
        {
            var count = Math.Min(LinesPerPage, lines.Length - i);
            pages.Add(string.Join('\n', lines, i, count));
        }
        return pages.Count == 0 ? [""] : [.. pages];
    }
}
