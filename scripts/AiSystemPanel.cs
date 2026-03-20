using Godot;

namespace Tts;

public partial class AiSystemPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 12f;
	private const string VisualScenePath = "res://scenes/AiSystemPanel.tscn";

	private Label _nameLabel = null!;
	private Label _dispositionLabel = null!;
	private Label _descriptionLabel = null!;
	private Label _barkLabel = null!;
	private AiConfig _aiConfig = null!;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(PanelWidth, 0f);

		var scene = GD.Load<PackedScene>(VisualScenePath);
		if (scene != null)
			AddChild(scene.Instantiate());

		var vbox = new VBoxContainer();
		AddChild(vbox);

		_nameLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_nameLabel.AddThemeFontSizeOverride("font_size", 12);
		vbox.AddChild(_nameLabel);

		_dispositionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_dispositionLabel.AddThemeFontSizeOverride("font_size", 9);
		vbox.AddChild(_dispositionLabel);

		_descriptionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_descriptionLabel.AddThemeFontSizeOverride("font_size", 10);
		vbox.AddChild(_descriptionLabel);

		_barkLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_barkLabel.AddThemeFontSizeOverride("font_size", 9);
		vbox.AddChild(_barkLabel);

		_aiConfig = ConfigLoader.Load<AiConfig>("res://config/ai.json");
		Visible = false;
	}

	public void ShowFor(AiPlayerData aiPlayer, Color dispositionColor, int seed, Vector2 viewportSize)
	{
		_nameLabel.Text = aiPlayer.FactionName;
		_nameLabel.AddThemeColorOverride("font_color", dispositionColor);

		_dispositionLabel.Text = $"{aiPlayer.Disposition} — {aiPlayer.Name}";
		_dispositionLabel.AddThemeColorOverride("font_color", dispositionColor);

		var descriptions = _aiConfig.DispositionDescriptions[aiPlayer.Disposition.ToString()];
		_descriptionLabel.Text = descriptions[seed % descriptions.Length];

		var barks = _aiConfig.DispositionBarks[aiPlayer.Disposition.ToString()];
		_barkLabel.Text = $"\"{barks[seed % barks.Length]}\"";
		_barkLabel.AddThemeColorOverride("font_color", new Color(dispositionColor.R, dispositionColor.G, dispositionColor.B, 0.65f));

		Visible = true;
		Position = new Vector2((viewportSize.X - PanelWidth) / 2f, TopPadding);
	}
}
