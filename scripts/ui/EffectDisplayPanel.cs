using System.Collections.Generic;
using Godot;
using Tts.Events;

namespace Tts.Ui;

// CanvasLayer overlaid at the bottom-left of the screen.
// Shows all active buffs/debuffs from EffectRegistry as coloured labels.
// Instantiated programmatically by Level — no scene file required.
public partial class EffectDisplayPanel : CanvasLayer
{
    private static readonly Color BuffColor   = new(0.45f, 1f,   0.55f);
    private static readonly Color DebuffColor = new(1f,   0.4f,  0.4f);
    private const float PaddingBottom = 10f;
    private const float PaddingLeft   = 8f;

    private VBoxContainer _container = null!;
    private readonly List<Label> _rows = [];

    public override void _Ready()
    {
        Layer = 8;

        var anchor = new Control();
        anchor.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        anchor.Position = new Vector2(PaddingLeft, -PaddingBottom);
        AddChild(anchor);

        _container = new VBoxContainer();
        _container.GrowVertical = Control.GrowDirection.Begin;
        _container.CustomMinimumSize = new Vector2(200f, 0f);
        anchor.AddChild(_container);
    }

    public void Refresh(IReadOnlyList<ActiveEffect> effects)
    {
        foreach (var lbl in _rows) lbl.QueueFree();
        _rows.Clear();

        foreach (var effect in effects)
        {
            var lbl = new Label();
            lbl.Text = $"{(effect.Kind == EffectKind.Buff ? "▲" : "▼")} {effect.Name}";
            lbl.TooltipText = effect.Description;
            lbl.AddThemeColorOverride("font_color",
                effect.Kind == EffectKind.Buff ? BuffColor : DebuffColor);
            _container.AddChild(lbl);
            _rows.Add(lbl);
        }
    }
}
