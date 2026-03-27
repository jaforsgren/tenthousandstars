using System;
using Godot;

namespace Tts;

[Tool]
public partial class InterludePanel : Control
{
    private Label _memoText = null!;
    private Label _dismissHint = null!;

    private Action? _onDismiss;
    private bool _active;

    private bool _previewInEditor;

    [Export]
    public bool PreviewInEditor
    {
        get => _previewInEditor;
        set
        {
            _previewInEditor = value;
            if (Engine.IsEditorHint() && IsNodeReady())
                ApplyEditorPreview();
        }
    }

    public override void _Ready()
    {
        _memoText = GetNode<Label>("%MemoText");
        _dismissHint = GetNode<Label>("%DismissHint");

        if (Engine.IsEditorHint())
        {
            ApplyEditorPreview();
            return;
        }

        Visible = false;
    }

    public void Show(string text, Vector2 viewportSize, Action onDismiss)
    {
        _memoText.Text = text;
        _onDismiss = onDismiss;
        _active = true;
        Size = viewportSize;
        Visible = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active) return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Dismiss();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Dismiss()
    {
        if (!_active) return;
        _active = false;
        Visible = false;
        var callback = _onDismiss;
        _onDismiss = null;
        callback?.Invoke();
    }

    private void ApplyEditorPreview()
    {
        if (!_previewInEditor) return;
        _memoText.Text = "The Last Command — Internal Memo\nSequence 1-2847\n\nThe situation has deteriorated faster than we anticipated. The Enemy moves through our outer systems with impunity.\n\nThis engagement at Kepler is not optional. We go to eliminate all enemy fleets.\n\nWe do not know what they have in store for us. We put our faith in our commanders.";
        _dismissHint.Text = "Tap to continue";
    }
}
