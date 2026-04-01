using System;

namespace Tts;

public record Bark(string[] Tags, StateCondition? When, string Npc, string Message);

public record BarkConfig(Bark[] Barks)
{
    public Bark[] Get(string tag)
    {
        var result = new System.Collections.Generic.List<Bark>();
        foreach (var bark in Barks)
            foreach (var t in bark.Tags)
                if (t == tag) { result.Add(bark); break; }
        return result.ToArray();
    }
}
