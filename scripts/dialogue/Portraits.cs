using System;
using Godot;

namespace Tts.Dialogue;

[Tool]
public partial class Portraits : Control
{
    public bool ShowPortrait(string speakerName)
    {
        bool found = false;
        foreach (Node child in GetChildren())
        {
            if (child is not TextureRect portrait) continue;
            bool match = portrait.Name.ToString().Equals(speakerName, StringComparison.OrdinalIgnoreCase);
            portrait.Visible = match;
            if (match) found = true;
        }
        return found;
    }

    public void HideAll()
    {
        foreach (Node child in GetChildren())
            if (child is TextureRect portrait)
                portrait.Visible = false;
    }
}
