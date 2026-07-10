using Godot;

namespace Tts.Ui;

enum SpeedChange
{
	SUBTRACT,
	ADD,
	RESET
}

public partial class SpeedControlPanel : PanelContainer
{
	[Export]
	public Texture2D PauseIcon { get; set; } = null!;

	[Export]
	public Texture2D PlayIcon { get; set; } = null!;

	private Label _speedLabel = null!;
	private Button _playPauseButton = null!;
	private Button _halfSpeedButton = null!;
	private Button _normalSpeedButton = null!;
	private Button _fastSpeedButton = null!;

	public override void _Ready()
	{
		_speedLabel = GetNode<Label>("%SpeedLabel");
		_playPauseButton = GetNode<Button>("%PlayPauseButton");
		_halfSpeedButton = GetNode<Button>("%HalfSpeedButton");
		//_normalSpeedButton = GetNode<Button>("%NormalSpeedButton");
		_fastSpeedButton = GetNode<Button>("%FastSpeedButton");

		_playPauseButton.Pressed += OnPlayPausePressed;
		_halfSpeedButton.Pressed += () => OnSpeedPressed(SpeedChange.SUBTRACT);
		//_normalSpeedButton.Pressed += () => OnSpeedPressed(SpeedChange.RESET);
		_fastSpeedButton.Pressed += () => OnSpeedPressed(SpeedChange.ADD);

		RefreshIndicator();
	}

	private void OnPlayPausePressed()
	{
		GameSpeed.SetPlayerPaused(!GameSpeed.IsPlayerPaused);
		RefreshIndicator();
	}

	private void OnSpeedPressed(SpeedChange change)
	{
		
		if (change == SpeedChange.SUBTRACT && GameSpeed.Speed == 4f )
		{
			GameSpeed.SetSpeed(1f);
		}
		
		if (change == SpeedChange.SUBTRACT && GameSpeed.Speed == 1f )
		{
			GameSpeed.SetSpeed(0.5f);
		}
		
		if (change == SpeedChange.ADD && GameSpeed.Speed == 1f )
		{
			GameSpeed.SetSpeed(4.0f);
		}
		
		if (change == SpeedChange.ADD && GameSpeed.Speed == 0.5f )
		{
			GameSpeed.SetSpeed(1.0f);
		}
		
		if (GameSpeed.IsPlayerPaused)
			GameSpeed.SetPlayerPaused(false);
		RefreshIndicator();
	}

	private void RefreshIndicator()
	{
		if (GameSpeed.IsPlayerPaused)
		{
			_playPauseButton.Icon = PlayIcon;
			_speedLabel.Text = "||";
		}
		else
		{
			_playPauseButton.Icon = PauseIcon;
			_speedLabel.Text = GameSpeed.Speed switch
			{
				0.5f => "½×",
				4.0f => "4×",
				_ => "1×"
			};
		}
	}
}
