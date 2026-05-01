using System;
using System.Collections.Generic;
using Godot;

namespace Tts;

// Screen-space intent picker that tracks a world position and sits beside the system.
// Required mode (arrival picker) ignores HideIfOptional — player must make a choice.
public partial class IntentPickerMenu : Control
{
    private const float ButtonWidth  = 80f;
    private const float ButtonHeight = 32f;
    private const float ButtonGap    = 4f;
    private const float SystemMargin = 12f; // gap between system edge and buttons
    private const float EdgeMargin   = 8f;  // minimum distance from viewport edge

    private readonly List<Button> _buttons = [];
    private CameraController _camera = null!;
    private Vector2 _trackedWorldPos;
    private float _systemRadius;
    private bool _required;

    public void Initialize(CameraController camera)
    {
        _camera = camera;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        Visible = false;
    }

    public void ShowAt(
        Vector2 worldPos,
        float systemRadius,
        (string Label, IntentType Intent)[] options,
        Action<IntentType> onPick,
        bool required = false)
    {
        ClearButtons();
        _trackedWorldPos = worldPos;
        _systemRadius = systemRadius;
        _required = required;

        foreach (var (label, intent) in options)
        {
            var btn = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight),
                MouseFilter = MouseFilterEnum.Stop,
                ClipContents = false
            };
            UILayout.ApplyGreyStyle(btn);
            var captured = intent;
            btn.Pressed += () => { onPick(captured); HideMenu(); };
            AddChild(btn);
            _buttons.Add(btn);
        }

        Visible = true;
        UpdateLayout();
    }

    // Always hides — used for game end or explicit cancel.
    public void HideMenu()
    {
        ClearButtons();
        _required = false;
        Visible = false;
    }

    // Hides only when not required — used by HideContextMenus on outside clicks.
    public void HideIfOptional()
    {
        if (_required) return;
        HideMenu();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        if (_buttons.Count == 0) return;

        var screenPos = GetViewport().GetCanvasTransform() * _trackedWorldPos;
        var viewport  = GetViewport().GetVisibleRect();
        var zoom      = _camera.Zoom.X;

        var screenRadius  = _systemRadius * zoom;
        var stackHeight   = _buttons.Count * ButtonHeight + (_buttons.Count - 1) * ButtonGap;

        // Prefer right side; fall back to left if it would clip the edge.
        var xRight = screenPos.X + screenRadius + SystemMargin;
        var x = xRight + ButtonWidth <= viewport.Size.X - EdgeMargin
            ? xRight
            : screenPos.X - screenRadius - SystemMargin - ButtonWidth;

        // Centre vertically on the system, then clamp within viewport.
        var y = screenPos.Y - stackHeight / 2f;
        y = Math.Clamp(y, EdgeMargin, viewport.Size.Y - stackHeight - EdgeMargin);

        // Clamp x as final safety.
        x = Math.Clamp(x, EdgeMargin, viewport.Size.X - ButtonWidth - EdgeMargin);

        for (var i = 0; i < _buttons.Count; i++)
            _buttons[i].Position = new Vector2(x, y + i * (ButtonHeight + ButtonGap));
    }

    private void ClearButtons()
    {
        foreach (var btn in _buttons)
            btn.QueueFree();
        _buttons.Clear();
    }
}
