using System;
using Godot;

namespace Tts.Ui;

public partial class UpgradeButtonNode : ActionButtonNodeBase
{
	protected override NodePath ButtonNodePath => "%UpgradeButton";

	public void Configure(bool isActive, bool disabled, Action? onPressed)
	{
		Show(onPressed);
		_button.ButtonPressed = isActive;
		_button.Disabled = disabled;
	}
}
