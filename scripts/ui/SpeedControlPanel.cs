using Godot;

namespace Tts;

public partial class SpeedControlPanel : PanelContainer
{
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
        _normalSpeedButton = GetNode<Button>("%NormalSpeedButton");
        _fastSpeedButton = GetNode<Button>("%FastSpeedButton");

        _playPauseButton.Pressed += OnPlayPausePressed;
        _halfSpeedButton.Pressed += () => OnSpeedPressed(0.5f);
        _normalSpeedButton.Pressed += () => OnSpeedPressed(1.0f);
        _fastSpeedButton.Pressed += () => OnSpeedPressed(4.0f);

        RefreshIndicator();
    }

    private void OnPlayPausePressed()
    {
        GameSpeed.SetPlayerPaused(!GameSpeed.IsPlayerPaused);
        RefreshIndicator();
    }

    private void OnSpeedPressed(float speed)
    {
        GameSpeed.SetSpeed(speed);
        if (GameSpeed.IsPlayerPaused)
            GameSpeed.SetPlayerPaused(false);
        RefreshIndicator();
    }

    private void RefreshIndicator()
    {
        if (GameSpeed.IsPlayerPaused)
        {
            _playPauseButton.Text = "▶";
            _speedLabel.Text = "⏸";
        }
        else
        {
            _playPauseButton.Text = "⏸";
            _speedLabel.Text = GameSpeed.Speed switch
            {
                0.5f => "½×",
                4.0f => "4×",
                _ => "1×"
            };
        }
    }
}
