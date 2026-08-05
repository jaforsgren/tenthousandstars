using System;
using Godot;

namespace Tts.Ui;

public partial class RerouteButtonNode : ActionButtonNodeBase
{
	protected override NodePath ButtonNodePath => "%RerouteButton";
	protected override bool UseGreyStyle => false;

	public void Configure(bool hasActiveRoute, Action onPressed)
	{
		Show(onPressed);
		_button.ButtonPressed = hasActiveRoute;
	}
}
