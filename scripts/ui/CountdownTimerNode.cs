using Godot;
using System;

namespace Tts.Ui;

public partial class CountdownTimerNode : Control
{
	private Label _labelTime = null!;
	private float _timeRemaining;
	private bool _active;
	private Action? _onExpire;

	public override void _Ready()
	{
		_labelTime = GetNode<Label>("%LabelTime");
	}

	public void Initialize(float seconds, Action onExpire)
	{
		_timeRemaining = seconds;
		_onExpire = onExpire;
		_active = true;
		UpdateDisplay();
	}

	public void Reset()
	{
		_active = false;
		_timeRemaining = 0f;
		_onExpire = null;
		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (!_active)
			return;

		_timeRemaining = Mathf.Max(0f, _timeRemaining - (float)delta);
		UpdateDisplay();

		if (_timeRemaining <= 0f)
		{
			_active = false;
			_onExpire?.Invoke();
		}
	}

	private void UpdateDisplay()
	{
		var minutes = (int)(_timeRemaining / 60f);
		var seconds = (int)(_timeRemaining % 60f);
		_labelTime.Text = $"{minutes:D2}:{seconds:D2}";
	}
}
