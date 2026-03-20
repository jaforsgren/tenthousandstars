using Godot;

namespace Tts;

public partial class SelectionPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 12f;
	private const string VisualScenePath = "res://scenes/SelectionPanel.tscn";

	private Label _titleLabel = null!;
	private Label _descriptionLabel = null!;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(PanelWidth, 0f);

		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());

		var vbox = new VBoxContainer();
		AddChild(vbox);

		_titleLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_titleLabel.AddThemeFontSizeOverride("font_size", 13);
		vbox.AddChild(_titleLabel);

		_descriptionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_descriptionLabel.AddThemeFontSizeOverride("font_size", 10);
		vbox.AddChild(_descriptionLabel);

		Visible = false;
	}

	public void ShowAt(string title, string description, Vector2 viewportSize)
	{
		_titleLabel.Text = title;
		_descriptionLabel.Text = description;
		Visible = true;

		var x = (viewportSize.X - PanelWidth) / 2f;
		Position = new Vector2(x, TopPadding);
	}
}
