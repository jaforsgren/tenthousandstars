using System;
using System.Collections.Generic;

namespace Tts.Utils;

// Parses Yarn node headers (key: value pairs before ---) from raw .yarn text.
// Returns a map of node title → key/value header fields (including "title").
// Node order in the source file is preserved via insertion order of the outer dictionary.
public static class YarnHeaderParser
{
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Parse(string yarnText)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var lines = yarnText.Split('\n');

        Dictionary<string, string>? current = null;
        var inHeader = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line == "---")
            {
                inHeader = false;
                continue;
            }

            if (line == "===")
            {
                current = null;
                continue;
            }

            if (!inHeader && current == null)
            {
                if (!line.StartsWith("title:", StringComparison.Ordinal))
                    continue;

                var title = line["title:".Length..].Trim();
                current = new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = title };
                result[title] = current;
                inHeader = true;
                continue;
            }

            if (inHeader && current != null)
            {
                var sep = line.IndexOf(':', StringComparison.Ordinal);
                if (sep > 0)
                    current[line[..sep].Trim()] = line[(sep + 1)..].Trim();
            }
        }

        return result;
    }
}
