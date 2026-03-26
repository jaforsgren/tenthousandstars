using Godot;

namespace Tts;

public static class UILayout
{
	public const float ButtonSize = 48f;
	public const float ButtonMargin = 20f;
	public const float ButtonGap = 8f;

	public static Vector2 BottomRightButtonPosition(Vector2 viewportSize, int slotFromRight)
		=> new(
			viewportSize.X - ButtonSize * slotFromRight - ButtonMargin - ButtonGap * (slotFromRight - 1),
			viewportSize.Y - ButtonSize - ButtonMargin
		);
}
