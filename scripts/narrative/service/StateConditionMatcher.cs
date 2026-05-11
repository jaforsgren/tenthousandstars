namespace Tts.Narrative;

internal static class StateConditionMatcher
{
    internal static bool Matches(StateCondition? condition, StoryState state)
    {
        if (condition == null)
            return true;
        if (condition.EnemyIsWinning.HasValue && condition.EnemyIsWinning.Value != state.EnemyIsWinning)
            return false;
        if (condition.PlayerStrongerThanEnemy.HasValue && condition.PlayerStrongerThanEnemy.Value != state.PlayerStrongerThanEnemy)
            return false;
        return true;
    }
}
