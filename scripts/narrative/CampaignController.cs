using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Tts.Ai;
using Tts.Config;
using Tts.Utils;

namespace Tts.Narrative;

// Loads yarn/campaign.yarn and drives story mode progression.
// Held in GameSession.Campaign so it survives scene reloads between missions.
public class CampaignController
{
    private readonly IReadOnlyDictionary<string, CampaignNode> _nodes;
    private readonly NarrativeBarkSystem _barkSystem;
    private string _enemyFaction;

    public bool IsCampaignComplete { get; private set; }
    public CampaignNode Current { get; private set; }
    public string PlayerFaction { get; }
    public string EnemyFaction => _enemyFaction;

    private CampaignController(
        IReadOnlyDictionary<string, CampaignNode> nodes,
        NarrativeBarkSystem barkSystem,
        string playerFaction,
        string enemyFaction)
    {
        _nodes = nodes;
        _barkSystem = barkSystem;
        PlayerFaction = playerFaction;
        _enemyFaction = enemyFaction;
        Current = nodes.Values.First();
    }

    public static CampaignController Load(Random rng)
    {
        var yarnText = YarnLinePool.ReadResourceText("res://yarn/campaign.yarn");
        var headers = YarnHeaderParser.Parse(yarnText);

        var nodes = new Dictionary<string, CampaignNode>(StringComparer.Ordinal);
        foreach (var (title, header) in headers)
            nodes[title] = CampaignNode.FromHeader(header);

        foreach (var node in nodes.Values)
        {
            if (node.OnWin != null && !nodes.ContainsKey(node.OnWin))
                GD.PushError($"[CampaignController] '{node.Title}' on_win references unknown node '{node.OnWin}'");
            if (node.OnLoss != null && !nodes.ContainsKey(node.OnLoss))
                GD.PushError($"[CampaignController] '{node.Title}' on_loss references unknown node '{node.OnLoss}'");
        }

        var rawPools = YarnLinePool.Load("res://yarn/barks.yarn");
        var barkPools = rawPools.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray(),
            StringComparer.Ordinal);
        var barkSystem = new NarrativeBarkSystem(barkPools, rng);

        var aiNamingCfg = ConfigLoader.Load<AiNamingConfig>("res://config/ai_naming.json");
        var playerFaction = AiNaming.GenerateCharacter(aiNamingCfg, AiDisposition.Cautious, rng).FactionName;
        var enemyFaction  = AiNaming.GenerateCharacter(aiNamingCfg, AiDisposition.Aggressive, rng).FactionName;

        return new CampaignController(nodes, barkSystem, playerFaction, enemyFaction);
    }

    public void UpdateEnemyFaction(string factionName) => _enemyFaction = factionName;

    public void Advance(bool won)
    {
        var nextTitle = won ? Current.OnWin : Current.OnLoss;
        if (nextTitle == null || !_nodes.TryGetValue(nextTitle, out var next))
        {
            IsCampaignComplete = true;
            return;
        }
        Current = next;
    }

    public IReadOnlyDictionary<string, string> BuildVars() => new Dictionary<string, string>
    {
        ["$player_faction"] = PlayerFaction,
        ["$enemy_faction"]  = _enemyFaction,
    };

    public string? TryGetBark(BarkTrigger trigger) => _barkSystem.TryGetBark(trigger);
}
