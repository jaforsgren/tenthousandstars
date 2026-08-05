using System;
using System.Linq;
using Godot;
using Tts.Config;
using Tts.Types;
using Tts.Ui;
using Tts.Utils;

namespace Tts.Level;

public partial class Level
{
	private void SelectFleet(int systemIndex)
	{
		if (_systems[systemIndex].IsPlayerOwned)
		{
			var slot = _selectedFleetSlot;
			ShowPlayerMenu(systemIndex, slot, () => ShowFleetInfo(systemIndex));
		}
		else if (_systems[systemIndex].IsAiOwned)
		{
			_levelUi.SystemActionMenu.ShowForAiSystem(
				_systems[systemIndex].GlobalPosition,
				() => ShowFleetInfo(systemIndex),
				() => ShowAiSystemInfo(systemIndex));
		}
		else
		{
			_levelUi.SystemActionMenu.ShowInfoOnly(_systems[systemIndex].GlobalPosition, () => ShowFleetInfo(systemIndex));
		}
	}

	private void SelectSystem(int systemIndex)
	{
		if (_systems[systemIndex].IsPlayerOwned)
		{
			ShowPlayerMenu(systemIndex, 0, () => ShowSystemInfo(systemIndex));
		}
		else
		{
			_levelUi.SystemActionMenu.ShowInfoOnly(_systems[systemIndex].GlobalPosition, () => ShowSystemInfo(systemIndex));
		}
	}

	private void ShowPlayerMenu(int systemIndex, int fleetSlot, Action onInfo)
	{
		ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
		var splitDisabled = !_systems[systemIndex].HasFleet || _systems[systemIndex].GetFleetShips(fleetSlot) < 2f;
		_levelUi.SystemActionMenu.ShowForPlayer(
			_systems[systemIndex].GlobalPosition,
			onInfo,
			_rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex),
			fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
			ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge),
			splitDisabled, () => OnSplitButtonPressed(systemIndex, fleetSlot));
	}

	private void SelectAiSystem(int systemIndex)
	{
		_levelUi.SystemActionMenu.ShowForAiSystem(
			_systems[systemIndex].GlobalPosition,
			() => ShowSystemInfo(systemIndex),
			() => ShowAiSystemInfo(systemIndex));
	}

	private void ComputeUpgradeStates(
		int systemIndex,
		out bool fortifyActive, out bool fortifyDisabled,
		out bool forgeActive, out bool forgeDisabled)
	{
		var upgrade = _systems[systemIndex].Upgrade;
		var canAfford = _systems[systemIndex].Ships >= _levelCfg.UpgradeCost;
		fortifyActive = upgrade == SystemUpgrade.Fortify;
		fortifyDisabled = !fortifyActive && (upgrade != SystemUpgrade.None || !canAfford);
		forgeActive = upgrade == SystemUpgrade.Forge;
		forgeDisabled = !forgeActive && (upgrade != SystemUpgrade.None || !canAfford);
	}

	private void ShowAiSystemInfo(int systemIndex)
	{
		var owner = _systems[systemIndex].OwnerPlayer;
		var aiPlayer = _aiPlayers.FirstOrDefault(p => p.Owner == owner);
		if (aiPlayer == null) return;
		_aiColors.TryGetValue(owner, out var color);
		_levelUi.HideContextMenus();
		_levelUi.SelectionPanel.Hide();
		_levelUi.AiSystemPanel.ShowFor(aiPlayer, color, _systemLoreSeeds[systemIndex], GetViewport().GetVisibleRect().Size);
	}

	private void ShowFleetInfo(int systemIndex)
	{
		var prefix = _systems[systemIndex].IsPlayerOwned ? "player_fleet" : "neutral_fleet";
		var (title, description) = PickLore($"{prefix}_titles", $"{prefix}_descriptions", _fleetLoreSeeds[systemIndex], "Unknown Fleet");
		_levelUi.HideContextMenus();
		_levelUi.AiSystemPanel.Hide();
		_levelUi.SelectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var (title, description) = PickSystemLore(systemIndex);
		_levelUi.HideContextMenus();
		_levelUi.AiSystemPanel.Hide();

		var scenario = _scenarioRegistry.GetScenario(systemIndex);
		if (scenario != null)
		{
			_levelUi.SelectionPanel.ShowWithScenario(
				title, description, scenario.IntroText,
				onEnter: () => OpenScenario(systemIndex, scenario),
				onIgnore: () => DismissScenario(systemIndex),
				GetViewport().GetVisibleRect().Size);
		}
		else
		{
			_levelUi.SelectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
		}
	}

	private (string Title, string Description) PickSystemLore(int systemIndex)
	{
		if (systemIndex < _systemData.Count && _systemData[systemIndex] is { Name: not null } authored)
			return (authored.Name, authored.Description ?? "");
		return PickLore("system_titles", "system_descriptions", _systemLoreSeeds[systemIndex], "Unknown System");
	}

	private (string Title, string Description) PickLore(string titlePoolKey, string descriptionPoolKey, int seed, string fallbackTitle)
	{
		var titles = YarnLinePool.GetPool(_lorePools, titlePoolKey);
		var descriptions = YarnLinePool.GetPool(_lorePools, descriptionPoolKey);
		var title = titles.Length > 0 ? Pick(titles, seed) : fallbackTitle;
		var description = descriptions.Length > 0 ? Pick(descriptions, seed) : "";
		return (title, description);
	}

	private void OpenScenario(int systemIndex, ScenarioDefinition scenario)
	{
		_levelUi.SelectionPanel.Hide();
		_levelUi.HideContextMenus();
		_levelUi.ScenarioPanel.Show(scenario, GetViewport().GetVisibleRect().Size, onClose: () =>
		{
			DismissScenario(systemIndex);
		});
	}

	private void DismissScenario(int systemIndex)
	{
		_scenarioRegistry.DismissScenario(systemIndex);
		_systems[systemIndex].SetScenarioBadge(false);
		_levelUi.SelectionPanel.Hide();
	}

	private void DoUpgrade(int systemIndex, SystemUpgrade upgrade)
	{
		if (upgrade != SystemUpgrade.None)
			_systems[systemIndex].SpendShips(_levelCfg.UpgradeCost);
		_systems[systemIndex].ApplyUpgrade(upgrade);

		var tag = upgrade switch
		{
			SystemUpgrade.Forge   => "player_forge",
			SystemUpgrade.Fortify => "player_fortify",
			_ => null
		};
		if (tag != null) PostChat(tag);

		ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
		_levelUi.SystemActionMenu.RefreshUpgradeButtons(
			fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
			ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge));
	}

	private void OnSplitButtonPressed(int systemIndex, int fleetSlot)
	{
		_systems[systemIndex].SplitFleet(fleetSlot);
		var splitDisabled = _systems[systemIndex].GetFleetShips(fleetSlot) < 2f;
		_levelUi.SystemActionMenu.RefreshSplitButton(splitDisabled, () => OnSplitButtonPressed(systemIndex, fleetSlot));
	}

	private static string Pick(string[] pool, int seed) => pool[seed % pool.Length];
}
