using Godot;

namespace Tts.Ui;

public partial class SpeedControlPanel : PanelContainer
{
	[Export] public Texture2D PauseIcon { get; set; } = null!;
	[Export] public Texture2D PlayIcon  { get; set; } = null!;

	private static readonly float[] SpeedSteps = [0.25f, 1.0f, 2.0f, 4.0f];

	private Label  _speedLabel      = null!;
	private Button _decreaseButton  = null!;
	private Button _playPauseButton = null!;
	private Button _increaseButton  = null!;

	public override void _Ready()
	{
		_speedLabel = GetNode<Label>("%SpeedLabel");
		_decreaseButton  = GetNode<Button>("%DecreaseButton");
		_playPauseButton = GetNode<Button>("%PlayPauseButton");
		_increaseButton  = GetNode<Button>("%IncreaseButton");

		_decreaseButton.Pressed  += OnDecreasePressed;
		_playPauseButton.Pressed += OnPlayPausePressed;
		_increaseButton.Pressed  += OnIncreasePressed;

		RefreshIndicator();
	}

	private void OnPlayPausePressed()
	{
		GameSpeed.SetPlayerPaused(!GameSpeed.IsPlayerPaused);
		RefreshIndicator();
	}

	private void OnDecreasePressed()
	{
		var idx = SpeedStepIndex();
		if (idx > 0)
			GameSpeed.SetSpeed(SpeedSteps[idx - 1]);
		if (GameSpeed.IsPlayerPaused)
			GameSpeed.SetPlayerPaused(false);
		RefreshIndicator();
	}

	private void OnIncreasePressed()
	{
		var idx = SpeedStepIndex();
		if (idx < SpeedSteps.Length - 1)
			GameSpeed.SetSpeed(SpeedSteps[idx + 1]);
		if (GameSpeed.IsPlayerPaused)
			GameSpeed.SetPlayerPaused(false);
		RefreshIndicator();
	}

	private int SpeedStepIndex()
	{
		for (var i = 0; i < SpeedSteps.Length; i++)
			if (Mathf.IsEqualApprox(GameSpeed.Speed, SpeedSteps[i]))
				return i;
		return 1;
	}

	private void RefreshIndicator()
	{
		_playPauseButton.Icon = GameSpeed.IsPlayerPaused ? PlayIcon : PauseIcon;

		_speedLabel.Text = GameSpeed.IsPlayerPaused
			? "||"
			: GameSpeed.Speed switch
			{
				0.25f => "¼×",
				2.0f  => "2×",
				4.0f  => "4×",
				_     => "1×"
			};
	}
}
