using System;
using Godot;

namespace Tts;

[Tool]
public partial class NarrativePanel : Control
{
    private Label _titleLabel = null!;
    private HSeparator _titleSep = null!;
    private Label _bodyText = null!;
    private VBoxContainer _actionButtons = null!;
    private Button _newCampaignButton = null!;
    private Button _randomMissionsButton = null!;
    private Button _quitButton = null!;
    private Label _dismissHint = null!;
    private ColorRect _blackBackground = null!;

    private Action? _onDismiss;
    private bool _active;

    public event Action? NewCampaignPressed;
    public event Action? RandomMissionsPressed;
    public event Action? QuitPressed;

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
        _titleLabel = GetNode<Label>("%TitleLabel");
        _titleSep = GetNode<HSeparator>("ContentBox/ContentLayout/TitleSep");
        _bodyText = GetNode<Label>("%BodyText");
        _actionButtons = GetNode<VBoxContainer>("ContentBox/ContentLayout/ActionButtons");
        _newCampaignButton = GetNode<Button>("%NewCampaignButton");
        _randomMissionsButton = GetNode<Button>("%RandomMissionsButton");
        _quitButton = GetNode<Button>("%QuitButton");
        _dismissHint = GetNode<Label>("%DismissHint");
        _blackBackground = GetNode<ColorRect>("BlackBackground");

        _newCampaignButton.Pressed += () => NewCampaignPressed?.Invoke();
        _randomMissionsButton.Pressed += () => RandomMissionsPressed?.Invoke();
        _quitButton.Pressed += () => QuitPressed?.Invoke();

        if (Engine.IsEditorHint())
        {
            ApplyEditorPreview();
            return;
        }

        Visible = false;
    }

    public void ShowDismissable(string? title, string body, Vector2 viewportSize, Action onDismiss)
    {
        SetTitle(title);
        _bodyText.Text = body;
        _actionButtons.Visible = false;
        _dismissHint.Visible = true;
        _blackBackground.Visible = true;
        _onDismiss = onDismiss;
        _active = true;
        Size = viewportSize;
        Visible = true;
        GameSpeed.PushUiPause();
    }

    public void ShowWithActions(string? title, string body, Vector2 viewportSize)
    {
        SetTitle(title);
        _bodyText.Text = body;
        _actionButtons.Visible = true;
        _dismissHint.Visible = false;
        _blackBackground.Visible = true;
        Size = viewportSize;
        Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!_active || !Visible) return;
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
        GameSpeed.PopUiPause();
        var callback = _onDismiss;
        _onDismiss = null;
        callback?.Invoke();
    }

    private void SetTitle(string? title)
    {
        var hasTitle = !string.IsNullOrEmpty(title);
        _titleLabel.Text = title ?? "";
        _titleLabel.Visible = hasTitle;
        _titleSep.Visible = hasTitle;
    }

    private void ApplyEditorPreview()
    {
        if (!_previewInEditor) return;
        SetTitle("Stochastic Collapse");
        _bodyText.Text = "The Last Command — Internal Memo\nSequence 1-2847\n\nThe situation has deteriorated faster than we anticipated. The Enemy moves through our outer systems with impunity.\n\nThis engagement at Kepler is not optional. We go to eliminate all enemy fleets.\n\nWe do not know what they have in store for us. We put our faith in our commanders.";
        _dismissHint.Text = "Tap to continue";
        _actionButtons.Visible = false;
        _dismissHint.Visible = true;
    }
}
