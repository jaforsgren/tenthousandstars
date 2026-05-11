#nullable enable

namespace Tts.Dialogue;

public sealed record SkillCheckResult(
	string Skill,
	int Difficulty,
	int Die1,
	int Die2,
	int SkillValue,
	int Roll,
	int Total,
	bool Success
);
