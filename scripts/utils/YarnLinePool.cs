using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Tts.Utils;

// Parses a .yarn file into text pools keyed by node title.
// Lines starting with "//", "->", or "<<" are excluded.
// Blank lines are preserved in the returned arrays; callers decide whether to skip them.
public static class YarnLinePool
{
    public static Dictionary<string, string[]> Load(string resPath)
    {
        string text;
        if (Type.GetType("Godot.Engine, GodotSharp") != null)
        {
            text = Godot.FileAccess.GetFileAsString(resPath);
        }
        else
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var fsPath = Path.Combine(root, resPath.Replace("res://", ""));
            text = File.ReadAllText(fsPath);
        }
        return Parse(text);
    }

    public static Dictionary<string, string[]> Parse(string text)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string? title = null;
        var inBody = false;
        var lines = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("title:", StringComparison.Ordinal))
            {
                title = trimmed["title:".Length..].Trim();
                inBody = false;
                lines.Clear();
                continue;
            }

            if (trimmed == "---") { inBody = title != null; continue; }

            if (trimmed == "===")
            {
                if (title != null) result[title] = lines.ToArray();
                title = null;
                inBody = false;
                lines.Clear();
                continue;
            }

            if (!inBody) continue;
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("->", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("<<", StringComparison.Ordinal)) continue;

            lines.Add(trimmed);
        }

        return result;
    }

    // Returns non-empty lines from a node — for random-pick pools (barks, names, etc.).
    public static string[] GetPool(IReadOnlyDictionary<string, string[]> pools, string key)
        => pools.TryGetValue(key, out var lines)
            ? lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray()
            : [];

    // Returns the first non-empty line from a node — for single-value lookups.
    public static string? GetFirst(IReadOnlyDictionary<string, string[]> pools, string key)
        => pools.TryGetValue(key, out var lines)
            ? lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
            : null;

    // Joins non-empty lines with double newline — for paragraph-structured narrative text.
    public static string GetText(IReadOnlyDictionary<string, string[]> pools, string key)
        => pools.TryGetValue(key, out var lines)
            ? string.Join("\n\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            : string.Empty;
}
