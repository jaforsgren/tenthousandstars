using System;
using Godot;

namespace Tts.Ui;

public partial class InfoButton : ActionButtonNodeBase
{
	protected override NodePath ButtonNodePath => "%InfoButton";

	public void Configure(Action onPressed) => Show(onPressed);
}
