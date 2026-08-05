using Godot;

namespace Tts.Utils;

public static class TimeUtils
{
	// Seconds since engine start, as a double.
	public static double NowSec() => Time.GetTicksMsec() / 1000.0;
}
