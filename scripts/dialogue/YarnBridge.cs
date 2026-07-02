#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using Yarn.Markup;
using YarnSpinnerGodot;
using Tts.Events;

namespace Tts.Dialogue;

/// <summary>
/// Yarn Spinner dialogue presenter that bridges the Yarn runtime to the
/// custom dialogue UI via <see cref="YarnAdapter"/>.
///
/// Registered Yarn commands:
///   &lt;&lt;desc "…"&gt;&gt;              – update line descriptor text
///   &lt;&lt;skill "name" difficulty&gt;&gt; – run a skill check (pauses dialogue, shows result)
///   &lt;&lt;gameplay "scene_name"&gt;&gt;   – run a gameplay segment (pauses dialogue, stores result)
///
/// Add as a sibling of DialogueRunnerNode and set dialogueRunner to point at it.
/// Include this node in the runner's dialoguePresenters list.
/// </summary>
[GlobalClass]
public partial class YarnBridge : Node, DialoguePresenterBase
{
	[Export] public DialogueRunner? dialogueRunner;

	public YarnAdapter Adapter { get; } = new();

	public List<IActionMarkupHandler> ActionMarkupHandlers { get; } = [];

	private string _currentDescriptor = "";

	public override void _Ready()
	{
		if (!IsInstanceValid(dialogueRunner))
		{
			GD.PushError($"{nameof(YarnBridge)}: dialogueRunner is not assigned.");
			return;
		}

		dialogueRunner!.AddCommandHandler("desc",
			new Action<string>(descriptor => { _currentDescriptor = descriptor; }));

		// <<skill "logic" 10>>
		// Yarn passes numbers as floats; we accept float and cast to int.
		dialogueRunner!.AddCommandHandler("skill",
			new Func<string, float, Task>(HandleSkillCommand));

		// <<gameplay "test_minigame">>
		dialogueRunner!.AddCommandHandler("gameplay",
			new Func<string, Task>(HandleGameplayCommand));

		// <<apply_effect "effect_id">>
		dialogueRunner!.AddCommandHandler("apply_effect",
			new Action<string>(id => EffectRegistry.Instance?.Apply(id)));

		// <<remove_effect "effect_id">>
		dialogueRunner!.AddCommandHandler("remove_effect",
			new Action<string>(id => EffectRegistry.Instance?.Remove(id)));

		// <<chain_event "yarn_node">>
		dialogueRunner!.AddCommandHandler("chain_event",
			new Action<string>(node => LevelEventController.Instance?.QueueChain(node)));

		// <<modify_encounter_chance 0.05>>
		dialogueRunner!.AddCommandHandler("modify_encounter_chance",
			new Action<float>(delta => LevelEventController.Instance?.ModifyEncounterChance(delta)));
	}

	// ── Yarn Presenter ──────────────────────────────────────────────────────

	public YarnTask OnDialogueStartedAsync()
	{
		SyncGameStateToYarn();
		return YarnTask.CompletedTask;
	}

	public YarnTask OnDialogueCompleteAsync()
	{
		_currentDescriptor = "";
		Adapter.CancelOptions();
		return YarnTask.CompletedTask;
	}

	public async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
	{
		MarkupParseResult markup = line.TextWithoutCharacterName;
		MarkupParser.ParseResult parsed = MarkupParser.Parse(markup);

		YarnLine yarnLine = new(
			Speaker: line.CharacterName ?? "",
			Descriptor: _currentDescriptor,
			ParsedText: parsed
		);

		await Adapter.EmitLineAndWait(yarnLine);
	}

	public async YarnTask<DialogueOption?> RunOptionsAsync(
		DialogueOption[] dialogueOptions,
		CancellationToken cancellationToken)
	{
		YarnOption[] options = dialogueOptions
			.Where(o => o.IsAvailable)
			.Select(o => new YarnOption(o.Line.TextWithoutCharacterName.Text, o.DialogueOptionID))
			.ToArray();

		int selectedId = await Adapter.EmitOptionsAndWait(options);

		if (cancellationToken.IsCancellationRequested)
			return null;

		return dialogueOptions.FirstOrDefault(o => o.DialogueOptionID == selectedId);
	}

	// ── Command Handlers ────────────────────────────────────────────────────

	private async Task HandleSkillCommand(string skillName, float difficulty)
	{
		if (GameState.Instance is not { } state)
		{
			GD.PushError("[YarnBridge] GameState.Instance is null during skill check.");
			return;
		}

		int skillValue = state.GetSkill(skillName);
		SkillCheckResult result = SkillSystem.Roll(skillName, skillValue, (int)difficulty);

		state.AddHistory(
			$"Skillsss check [{skillName}]: " +
			$"({result.Die1}+{result.Die2}) + {result.SkillValue} = {result.Total} " +
			$"vs {result.Difficulty} → {(result.Success ? "SUCCESS" : "FAILURE")}");

		// Store results in Yarn variables so scripts can branch on them
		dialogueRunner!.VariableStorage.SetValue("$last_skill_success", result.Success);
		dialogueRunner!.VariableStorage.SetValue("$last_skill_total", (float)result.Total);
		dialogueRunner!.VariableStorage.SetValue("$last_skill_name", skillName);

		// Show the visual entry and wait for it to finish fading in
		await Adapter.EmitSkillCheckAndWait(result);
	}

	private async Task HandleGameplayCommand(string sceneName)
	{
		if (GameManager.Instance is not { } manager)
		{
			GD.PushError("[YarnBridge] GameManager.Instance is null during gameplay command.");
			return;
		}

		Dictionary result = await manager.RunGameplay(sceneName);

		GameState.Instance?.AddHistory($"Gameplay [{sceneName}]: {result}");

		// Sync every result key into a Yarn variable ($key) and GameState flag
		foreach (Variant key in result.Keys)
		{
			string varName = $"${key.AsString()}";
			Variant value = result[key];

			switch (value.VariantType)
			{
				case Variant.Type.Bool:
					dialogueRunner!.VariableStorage.SetValue(varName, value.AsBool());
					GameState.Instance?.SetFlag(key.AsString(), value);
					break;

				case Variant.Type.Int:
					dialogueRunner!.VariableStorage.SetValue(varName, (float)value.AsInt32());
					GameState.Instance?.SetFlag(key.AsString(), value);
					break;

				case Variant.Type.Float:
					dialogueRunner!.VariableStorage.SetValue(varName, value.AsSingle());
					GameState.Instance?.SetFlag(key.AsString(), value);
					break;

				case Variant.Type.String:
					dialogueRunner!.VariableStorage.SetValue(varName, value.AsString());
					GameState.Instance?.SetFlag(key.AsString(), value);
					break;
			}
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private void SyncGameStateToYarn()
	{
		if (GameState.Instance is not { } state || !IsInstanceValid(dialogueRunner))
			return;

		var storage = dialogueRunner!.VariableStorage;

		// Push all skill values so Yarn scripts can read them
		foreach ((string skill, int value) in state.Skills)
			storage.SetValue($"${skill}", (float)value);

		// Push previously-set flags back (e.g. gameplay results from a prior run)
		foreach ((string key, Variant value) in state.Flags)
		{
			switch (value.VariantType)
			{
				case Variant.Type.Bool: storage.SetValue($"${key}", value.AsBool()); break;
				case Variant.Type.Int: storage.SetValue($"${key}", (float)value.AsInt32()); break;
				case Variant.Type.Float: storage.SetValue($"${key}", value.AsSingle()); break;
				case Variant.Type.String: storage.SetValue($"${key}", value.AsString()); break;
			}
		}
	}
}
