using Godot;
using System;

namespace Tts;

public static class GameSpeed
{
    private static float _speed = 1.0f;
    private static bool _playerPaused;
    private static int _uiPauseDepth;

    public static bool IsPlayerPaused => _playerPaused;
    public static float Speed => _speed;

    public static void SetSpeed(float speed)
    {
        _speed = speed;
        Apply();
    }

    public static void SetPlayerPaused(bool paused)
    {
        _playerPaused = paused;
        Apply();
    }

    public static void PushUiPause()
    {
        _uiPauseDepth++;
        Apply();
    }

    public static void PopUiPause()
    {
        _uiPauseDepth = Math.Max(0, _uiPauseDepth - 1);
        Apply();
    }

    public static void Reset()
    {
        _speed = 1.0f;
        _playerPaused = false;
        _uiPauseDepth = 0;
        Apply();
    }

    private static void Apply()
    {
        Engine.TimeScale = (_playerPaused || _uiPauseDepth > 0) ? 0.0 : _speed;
    }
}
