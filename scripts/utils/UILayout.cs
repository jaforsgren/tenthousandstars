using Godot;

namespace Tts.Utils;

public static class UILayout
{
	public const float ButtonSize = 48f;

	private static readonly Color GreyNormal = new(0.55f, 0.55f, 0.55f, 1f);
	private static readonly Color GreyHover = new(0.65f, 0.65f, 0.65f, 1f);
	private static readonly Color GreyPressed = new(0.42f, 0.42f, 0.42f, 1f);
	private const int ButtonCornerRadius = 4;

	public static void ApplyGreyStyle(Button button)
	{
		button.AddThemeStyleboxOverride("normal", MakeFlat(GreyNormal));
		button.AddThemeStyleboxOverride("hover", MakeFlat(GreyHover));
		button.AddThemeStyleboxOverride("pressed", MakeFlat(GreyPressed));
	}

	private static StyleBoxFlat MakeFlat(Color color) => new()
	{
		BgColor = color,
		CornerRadiusTopLeft = ButtonCornerRadius,
		CornerRadiusTopRight = ButtonCornerRadius,
		CornerRadiusBottomLeft = ButtonCornerRadius,
		CornerRadiusBottomRight = ButtonCornerRadius,
		CornerDetail = 4
	};
}
