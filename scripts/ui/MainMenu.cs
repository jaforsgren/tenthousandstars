using System.Linq;
using Godot;
using Tts.Dialogue;
using Tts.Utils;

namespace Tts.Ui;

public partial class MainMenu : Control
{
	private Button _campaignButton = null!;
	private Button _randomMapButton = null!;
	private Button _exitButton = null!;
	private Portraits _portraits = null!;

	public override void _Ready()
	{
		_campaignButton  = GetNode<Button>("%CampaignButton");
		_randomMapButton = GetNode<Button>("%RandomMapButton");
		_exitButton      = GetNode<Button>("%ExitButton");
		_portraits       = GetNode<Portraits>("%Portraits");

		_campaignButton.Pressed  += OnCampaignPressed;
		_randomMapButton.Pressed += OnRandomMapPressed;
		_exitButton.Pressed     += OnExitPressed;

		ShowRandomPortrait();
	}

	private void ShowRandomPortrait()
	{
		var faces = _portraits.GetChildren().OfType<TextureRect>().ToArray();
		if (faces.Length == 0) return;

		_portraits.HideAll();
		faces[new System.Random().Next(faces.Length)].Visible = true;
	}

	private void OnCampaignPressed()
	{
		GameSession.Campaign = null;
		GameSession.GameModeOverride = GameMode.Story;
		GetTree().ChangeSceneToFile(ScenePaths.Level);
	}

	private void OnRandomMapPressed()
	{
		GameSession.Campaign = null;
		GameSession.GameModeOverride = GameMode.Skirmish;
		GetTree().ChangeSceneToFile(ScenePaths.Level);
	}

	private void OnExitPressed() => GetTree().Quit();
}
