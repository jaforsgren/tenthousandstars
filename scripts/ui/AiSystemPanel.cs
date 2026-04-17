using Godot;

namespace Tts;

[Tool]
public partial class AiSystemPanel : PanelContainer
{
	private const float PanelWidth = 200f;
	private const float TopPadding = 12f;

	private Label _nameLabel = null!;
	private Label _dispositionLabel = null!;
	private Label _descriptionLabel = null!;
	private Label _barkLabel = null!;
	private AiConfig _aiConfig = null!;

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

		_nameLabel = GetNode<Label>("%Title");
		_dispositionLabel = GetNode<Label>("%Title2");
		_descriptionLabel = GetNode<Label>("%Decription");
		_barkLabel = GetNode<Label>("%Bark");

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		_aiConfig = ConfigLoader.Load<AiConfig>("res://config/ai.json");
	}

	public void ShowFor(AiPlayerData aiPlayer, Color dispositionColor, int seed, Vector2 viewportSize)
	{
		_nameLabel.Text = aiPlayer.FactionName;
		_nameLabel.AddThemeColorOverride("font_color", dispositionColor);

		_dispositionLabel.Text = $"{aiPlayer.Disposition} — {aiPlayer.Title}";
		_dispositionLabel.AddThemeColorOverride("font_color", dispositionColor);

		_descriptionLabel.Text = aiPlayer.Description;

		_barkLabel.Text = aiPlayer.Barks.Length > 0
			? $"\"{aiPlayer.Barks[seed % aiPlayer.Barks.Length]}\""
			: string.Empty;
		_barkLabel.AddThemeColorOverride("font_color", dispositionColor.WithAlpha(0.65f));

		Visible = true;
		//Position = new Vector2((viewportSize.X - PanelWidth) / 2f, TopPadding);
	}

	private void ApplyEditorPreview()
	{
		if (_previewInEditor)
		{
			var previewColor = new Color(1f, 0.49f, 0.13f);
			_nameLabel.Text = "The Crimson Veil";
			_nameLabel.AddThemeColorOverride("font_color", previewColor);
			_dispositionLabel.Text = "Aggressive — Veilhammer";
			_dispositionLabel.AddThemeColorOverride("font_color", previewColor);
			_descriptionLabel.Text = "They expand through force alone. Every system taken by blood.";
			_barkLabel.Text = "\"Your kind is weak.\"";
			_barkLabel.AddThemeColorOverride("font_color", previewColor.WithAlpha(0.65f));
		}
		else
		{
			_nameLabel.Text = "Title";
			_nameLabel.RemoveThemeColorOverride("font_color");
			_dispositionLabel.Text = "Disposition : Name";
			_dispositionLabel.RemoveThemeColorOverride("font_color");
			_descriptionLabel.Text = "Description";
			_barkLabel.Text = "\"Bark\"";
			_barkLabel.RemoveThemeColorOverride("font_color");
		}
	}
}
