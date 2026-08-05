using System;
using Godot;

namespace Tts.Ui;

public partial class SplitButtonNode : ActionButtonNodeBase
{
	protected override NodePath ButtonNodePath => "%SplitButton";
	protected override bool UseGreyStyle => false;

	public void Configure(bool disabled, Action? onPressed)
	{
		Show(onPressed);
		_button.Disabled = disabled;
	}
}
