using System;
using System.Reflection;
using Xunit;

namespace Tts.Tests;

public sealed class NarrativeControllerTests
{
    // ── Stubs ────────────────────────────────────────────────────────────────

    private sealed class StubNarrativeService : INarrativeService
    {
        public StoryState CurrentState { get; set; } = DefaultState();
        public bool IsCampaignComplete { get; set; }
        public string? LastStartedArchetypeId { get; private set; }
        public Character? LastUpdatedEnemy { get; private set; }
        public MissionContext? MissionToReturn { get; set; }
        public MissionResult? LastMissionResult { get; private set; }

        public void StartCampaign(string archetypeId) => LastStartedArchetypeId = archetypeId;
        public void UpdateEnemy(Character enemy) => LastUpdatedEnemy = enemy;
        public MissionContext GetNextMission() => MissionToReturn!;
        public void OnMissionComplete(MissionResult result) => LastMissionResult = result;
    }

    private sealed class StubNarrativeBarkSystem : INarrativeBarkSystem
    {
        public string? BarkToReturn { get; set; }
        public BarkTrigger? LastTrigger { get; private set; }
        public StoryState? LastState { get; private set; }

        public string? TryGetBark(BarkTrigger trigger, StoryState state)
        {
            LastTrigger = trigger;
            LastState = state;
            return BarkToReturn;
        }
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static Character TestCharacter(
        string factionName = "Alpha Fleet",
        AiDisposition disposition = AiDisposition.Strategic)
        => new(disposition, "Test", factionName, "Test description.", Array.Empty<string>());

    private static StoryState DefaultState(
        int missionsCompleted = 0,
        int missionsWon = 0,
        int totalChapters = 3,
        int currentChapterIndex = 0,
        string currentChapterId = "skirmish",
        string archetypeId = "rising_power",
        string playerFaction = "Alpha Fleet",
        string enemyFaction = "The Dominion",
        bool lastMissionWon = false,
        bool enemyIsWinning = false,
        bool playerStronger = true)
        => new(missionsCompleted, missionsWon, totalChapters, currentChapterIndex,
               currentChapterId, archetypeId,
               TestCharacter(playerFaction), TestCharacter(enemyFaction),
               lastMissionWon, enemyIsWinning, playerStronger);

    private static MissionContext DefaultMissionContext(StoryState? state = null)
    {
        var condition = new NarrativeConditionConfig(
            "test_cond", Array.Empty<string>(),
            null, null, null, null, null, null, null,
            "Destroy all enemies", "All enemies destroyed");
        var chapter = new ChapterContext("ch1", "Opening Skirmish",
            Array.Empty<string>(), Array.Empty<string>());
        return new MissionContext(condition, "Briefing text.", chapter, state ?? DefaultState(), null);
    }

    private static OutroGenerator BuildOutroGenerator(string archetypeId = "rising_power")
    {
        var archetype = new ArchetypeConfig(
            archetypeId, "Rising Power",
            new[] { "ch1", "ch2", "ch3" },
            AiDisposition.Strategic,
            Array.Empty<string>());

        var db = new NarrativeDatabase(
            new[] { archetype },
            Array.Empty<ChapterDefConfig>(),
            Array.Empty<NarrativeConditionConfig>(),
            new BriefingConfig(Array.Empty<BriefingTemplate>()),
            new NarrativeBarkConfig(Array.Empty<NarrativeBark>()),
            new InterludeConfig(3000, "Year {year}", Array.Empty<string>(), Array.Empty<InterludeTemplate>()),
            new OutroConfig(new[]
            {
                new OutroTemplate("win_arch",  new[] { "win",  archetypeId }, "Triumph",     "{PlayerFaction} crushed {EnemyFaction} — {MissionsWon}/{TotalMissions} missions won."),
                new OutroTemplate("loss_arch", new[] { "loss", archetypeId }, "Fallen Banner", "{EnemyFaction} prevailed — {MissionsWon}/{TotalMissions} missions won."),
                new OutroTemplate("win_gen",   new[] { "win"               }, "Victory",       "Generic win."),
                new OutroTemplate("loss_gen",  new[] { "loss"              }, "Defeat",        "Generic loss."),
            }));

        return new OutroGenerator(db.Outro, db, new Random(42));
    }

    // Title tokens are intentionally NOT substituted by OutroGenerator — only the text body is.
    // Tests that check title equality must use the raw template string.

    private static NarrativeController CreateController(
        INarrativeService? service = null,
        INarrativeBarkSystem? barkSystem = null,
        OutroGenerator? outroGenerator = null)
    {
        service ??= new StubNarrativeService();
        barkSystem ??= new StubNarrativeBarkSystem();
        outroGenerator ??= BuildOutroGenerator();

        var ctor = typeof(NarrativeController).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(INarrativeService), typeof(INarrativeBarkSystem), typeof(OutroGenerator) },
            null)!;

        return (NarrativeController)ctor.Invoke(new object[] { service, barkSystem, outroGenerator });
    }

    // ── IsCampaignComplete ────────────────────────────────────────────────────

    [Fact]
    public void IsCampaignComplete_ReturnsFalse_WhenServiceReportsFalse()
    {
        var service = new StubNarrativeService { IsCampaignComplete = false };
        var controller = CreateController(service);

        Assert.False(controller.IsCampaignComplete);
    }

    [Fact]
    public void IsCampaignComplete_ReturnsTrue_WhenServiceReportsTrue()
    {
        var service = new StubNarrativeService { IsCampaignComplete = true };
        var controller = CreateController(service);

        Assert.True(controller.IsCampaignComplete);
    }

    // ── CurrentState ──────────────────────────────────────────────────────────

    [Fact]
    public void CurrentState_ReturnsExactStateFromService()
    {
        var expected = DefaultState(missionsCompleted: 2, missionsWon: 1, archetypeId: "conquest");
        var service = new StubNarrativeService { CurrentState = expected };
        var controller = CreateController(service);

        Assert.Equal(expected, controller.CurrentState);
    }

    [Fact]
    public void CurrentState_ReflectsUpdatesFromService()
    {
        var service = new StubNarrativeService { CurrentState = DefaultState() };
        var controller = CreateController(service);

        var updated = DefaultState(missionsCompleted: 5);
        service.CurrentState = updated;

        Assert.Equal(updated, controller.CurrentState);
    }

    // ── StartCampaign ─────────────────────────────────────────────────────────

    [Fact]
    public void StartCampaign_ForwardsArchetypeIdToService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.StartCampaign("conquest");

        Assert.Equal("conquest", service.LastStartedArchetypeId);
    }

    [Fact]
    public void StartCampaign_ForwardsEmptyStringToService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.StartCampaign("");

        Assert.Equal("", service.LastStartedArchetypeId);
    }

    [Fact]
    public void StartCampaign_DefaultsToEmptyStringArchetypeId()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.StartCampaign();

        Assert.Equal("", service.LastStartedArchetypeId);
    }

    // ── UpdateEnemy ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateEnemy_ForwardsCharacterToService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);
        var enemy = TestCharacter("The Dominion");

        controller.UpdateEnemy(enemy);

        Assert.Equal("The Dominion", service.LastUpdatedEnemy?.FactionName);
    }

    [Fact]
    public void UpdateEnemy_PreservesFullCharacterOnService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);
        var enemy = TestCharacter("Iron Pact", AiDisposition.Aggressive);

        controller.UpdateEnemy(enemy);

        Assert.Equal(enemy, service.LastUpdatedEnemy);
    }

    // ── GetNextMission ────────────────────────────────────────────────────────

    [Fact]
    public void GetNextMission_ReturnsMissionContextFromService()
    {
        var expected = DefaultMissionContext();
        var service = new StubNarrativeService { MissionToReturn = expected };
        var controller = CreateController(service);

        var result = controller.GetNextMission();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextMission_ReturnsMissionWithInterlude_WhenInterludePresent()
    {
        var context = DefaultMissionContext() with { Interlude = new InterludeContent("Battle of the Rim Worlds.") };
        var service = new StubNarrativeService { MissionToReturn = context };
        var controller = CreateController(service);

        var result = controller.GetNextMission();

        Assert.NotNull(result.Interlude);
        Assert.Equal("Battle of the Rim Worlds.", result.Interlude!.Text);
    }

    [Fact]
    public void GetNextMission_PreservesConditionFromService()
    {
        var condition = new NarrativeConditionConfig(
            "eliminate", new[] { "assault" },
            EnemiesLeft: 0, null, null, null, null, null, null,
            "Eliminate all enemies", "Done");
        var context = DefaultMissionContext() with { Condition = condition };
        var service = new StubNarrativeService { MissionToReturn = context };
        var controller = CreateController(service);

        var result = controller.GetNextMission();

        Assert.Equal("eliminate", result.Condition.Id);
        Assert.Equal(0, result.Condition.EnemiesLeft);
    }

    // ── OnMissionComplete ─────────────────────────────────────────────────────

    [Fact]
    public void OnMissionComplete_SendsWonTrueToService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: true, playerSystems: 5, enemySystems: 2, enemyFleets: 1);

        Assert.True(service.LastMissionResult!.Won);
    }

    [Fact]
    public void OnMissionComplete_SendsWonFalseToService()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: false, playerSystems: 1, enemySystems: 8, enemyFleets: 3);

        Assert.False(service.LastMissionResult!.Won);
    }

    [Fact]
    public void OnMissionComplete_ForwardsPlayerSystemCount()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: true, playerSystems: 7, enemySystems: 2, enemyFleets: 0);

        Assert.Equal(7, service.LastMissionResult!.PlayerSystemCount);
    }

    [Fact]
    public void OnMissionComplete_ForwardsEnemySystemCount()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: false, playerSystems: 1, enemySystems: 9, enemyFleets: 4);

        Assert.Equal(9, service.LastMissionResult!.EnemySystemCount);
    }

    [Fact]
    public void OnMissionComplete_ForwardsEnemyFleetCount()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: true, playerSystems: 4, enemySystems: 0, enemyFleets: 6);

        Assert.Equal(6, service.LastMissionResult!.EnemyFleetCount);
    }

    [Fact]
    public void OnMissionComplete_ForwardsZeroCountsCorrectly()
    {
        var service = new StubNarrativeService();
        var controller = CreateController(service);

        controller.OnMissionComplete(won: true, playerSystems: 0, enemySystems: 0, enemyFleets: 0);

        var result = service.LastMissionResult!;
        Assert.Equal(0, result.PlayerSystemCount);
        Assert.Equal(0, result.EnemySystemCount);
        Assert.Equal(0, result.EnemyFleetCount);
    }

    // ── GenerateOutro ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateOutro_ReturnsTitleAndText()
    {
        var state = DefaultState(missionsWon: 2, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (title, text) = controller.GenerateOutro();

        Assert.False(string.IsNullOrEmpty(title));
        Assert.False(string.IsNullOrEmpty(text));
    }

    [Fact]
    public void GenerateOutro_ReturnsWinTemplate_WhenMajorityOfMissionsWon()
    {
        // MissionsWon * 2 >= TotalChapters → win branch
        var state = DefaultState(missionsWon: 2, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (title, _) = controller.GenerateOutro();

        Assert.Equal("Triumph", title);
    }

    [Fact]
    public void GenerateOutro_ReturnsLossTemplate_WhenMinorityOfMissionsWon()
    {
        // MissionsWon * 2 < TotalChapters → loss branch
        var state = DefaultState(missionsWon: 1, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (title, _) = controller.GenerateOutro();

        Assert.Equal("Fallen Banner", title);
    }

    [Fact]
    public void GenerateOutro_SubstitutesPlayerFactionToken()
    {
        var state = DefaultState(missionsWon: 2, totalChapters: 3,
            archetypeId: "rising_power", playerFaction: "Nova Corps");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (_, text) = controller.GenerateOutro();

        Assert.Contains("Nova Corps", text);
    }

    [Fact]
    public void GenerateOutro_SubstitutesEnemyFactionToken()
    {
        var state = DefaultState(missionsWon: 1, totalChapters: 3,
            archetypeId: "rising_power", enemyFaction: "Iron Pact");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (_, text) = controller.GenerateOutro();

        Assert.Contains("Iron Pact", text);
    }

    [Fact]
    public void GenerateOutro_SubstitutesMissionsWonToken()
    {
        var state = DefaultState(missionsWon: 2, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (_, text) = controller.GenerateOutro();

        Assert.Contains("2", text);
    }

    [Fact]
    public void GenerateOutro_SubstitutesTotalMissionsToken()
    {
        var state = DefaultState(missionsWon: 2, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = state };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        var (_, text) = controller.GenerateOutro();

        Assert.Contains("3", text);
    }

    [Fact]
    public void GenerateOutro_UsesCurrentStateFromService_NotStaleSnapshot()
    {
        // State changes between construction and calling GenerateOutro.
        // The controller must read from the service at call time, not cache early.
        var initial = DefaultState(missionsWon: 0, totalChapters: 3, archetypeId: "rising_power");
        var service = new StubNarrativeService { CurrentState = initial };
        var controller = CreateController(service, outroGenerator: BuildOutroGenerator("rising_power"));

        // Win state assigned after controller construction
        service.CurrentState = DefaultState(missionsWon: 2, totalChapters: 3, archetypeId: "rising_power");

        var (title, _) = controller.GenerateOutro();

        Assert.Equal("Triumph", title);
    }

    // ── TryGetBark ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BarkTrigger.ChapterStart)]
    [InlineData(BarkTrigger.MissionWon)]
    [InlineData(BarkTrigger.MissionLost)]
    [InlineData(BarkTrigger.EnemyIsWinning)]
    [InlineData(BarkTrigger.PlayerLeading)]
    public void TryGetBark_ForwardsEveryTriggerValueToSystem(BarkTrigger trigger)
    {
        var barkSystem = new StubNarrativeBarkSystem { BarkToReturn = "test bark" };
        var controller = CreateController(barkSystem: barkSystem);

        controller.TryGetBark(trigger);

        Assert.Equal(trigger, barkSystem.LastTrigger);
    }

    [Fact]
    public void TryGetBark_ForwardsCurrentStateToSystem()
    {
        var state = DefaultState(missionsCompleted: 3, archetypeId: "falling_empire");
        var service = new StubNarrativeService { CurrentState = state };
        var barkSystem = new StubNarrativeBarkSystem();
        var controller = CreateController(service, barkSystem);

        controller.TryGetBark(BarkTrigger.MissionWon);

        Assert.Equal(state, barkSystem.LastState);
    }

    [Fact]
    public void TryGetBark_ReturnsBarkFromSystem()
    {
        var barkSystem = new StubNarrativeBarkSystem { BarkToReturn = "Nice work, Commander." };
        var controller = CreateController(barkSystem: barkSystem);

        var result = controller.TryGetBark(BarkTrigger.MissionWon);

        Assert.Equal("Nice work, Commander.", result);
    }

    [Fact]
    public void TryGetBark_ReturnsNull_WhenSystemReturnsNull()
    {
        var barkSystem = new StubNarrativeBarkSystem { BarkToReturn = null };
        var controller = CreateController(barkSystem: barkSystem);

        var result = controller.TryGetBark(BarkTrigger.MissionLost);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetBark_ReflectsUpdatedStateAtCallTime()
    {
        var initial = DefaultState(enemyIsWinning: false);
        var service = new StubNarrativeService { CurrentState = initial };
        var barkSystem = new StubNarrativeBarkSystem();
        var controller = CreateController(service, barkSystem);

        var updated = DefaultState(enemyIsWinning: true);
        service.CurrentState = updated;

        controller.TryGetBark(BarkTrigger.EnemyIsWinning);

        Assert.True(barkSystem.LastState!.EnemyIsWinning);
    }

    // ── PrintDebugState ───────────────────────────────────────────────────────

    [Fact]
    public void PrintDebugState_DoesNotThrow()
    {
        var service = new StubNarrativeService
        {
            CurrentState = DefaultState(
                missionsCompleted: 2, missionsWon: 1,
                currentChapterIndex: 1, totalChapters: 3,
                currentChapterId: "assault",
                archetypeId: "conquest",
                playerFaction: "Alpha Fleet",
                enemyFaction: "Iron Pact",
                enemyIsWinning: false,
                playerStronger: true)
        };
        var controller = CreateController(service);

        var exception = Record.Exception(() => controller.PrintDebugState());

        Assert.Null(exception);
    }
}
