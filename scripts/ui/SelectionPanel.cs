using Godot;

namespace Tts;

[Tool]
public partial class SelectionPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 12f;

	private Label _titleLabel = null!;
	private Label _descriptionLabel = null!;

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
		CustomMinimumSize = new Vector2(PanelWidth, 0f);

		_titleLabel = GetNode<Label>("%Title");
		_descriptionLabel = GetNode<Label>("%Description");

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		Visible = false;
	}

	public void ShowAt(string title, string description, Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		Visible = true;
		Position = new Vector2((viewportSize.X - PanelWidth) / 2f, TopPadding);
	}

	private void ApplyEditorPreview()
	{
		if (_previewInEditor)
		{
			_titleLabel.Text = "Vantara Prime";
			_descriptionLabel.Text = "A contested frontier world. Rich in ore deposits but scarred by old campaigns.";
		}
		else
		{
			_titleLabel.Text = "System Name";
			_descriptionLabel.Text = "Description text goes here.";
		}
	}
}
