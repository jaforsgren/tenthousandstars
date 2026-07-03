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
			ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
			var slot = _selectedFleetSlot;
			var splitDisabled = _systems[systemIndex].GetFleetShips(slot) < 2f;
			_levelUi.SystemActionMenu.ShowForPlayer(
				_systems[systemIndex].GlobalPosition,
				() => ShowFleetInfo(systemIndex),
				_rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex),
				fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
				ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge),
				splitDisabled, () => OnSplitButtonPressed(systemIndex, slot));
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
			ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
			var splitDisabled = !_systems[systemIndex].HasFleet || _systems[systemIndex].GetFleetShips(0) < 2f;
			_levelUi.SystemActionMenu.ShowForPlayer(
				_systems[systemIndex].GlobalPosition,
				() => ShowSystemInfo(systemIndex),
				_rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex),
				fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
				ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge),
				splitDisabled, () => OnSplitButtonPressed(systemIndex, 0));
		}
		else
		{
			_levelUi.SystemActionMenu.ShowInfoOnly(_systems[systemIndex].GlobalPosition, () => ShowSystemInfo(systemIndex));
		}
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
		_levelUi.SelectionPanel.Hide();
		_levelUi.AiSystemPanel.ShowFor(aiPlayer, color, _systemLoreSeeds[systemIndex], GetViewport().GetVisibleRect().Size);
	}

	private void ShowFleetInfo(int systemIndex)
	{
		var prefix = _systems[systemIndex].IsPlayerOwned ? "player_fleet" : "neutral_fleet";
		var seed = _fleetLoreSeeds[systemIndex];
		var titles = YarnLinePool.GetPool(_lorePools, $"{prefix}_titles");
		var descriptions = YarnLinePool.GetPool(_lorePools, $"{prefix}_descriptions");
		var title = titles.Length > 0 ? Pick(titles, seed) : "Unknown Fleet";
		var description = descriptions.Length > 0 ? Pick(descriptions, seed) : "";
		_levelUi.AiSystemPanel.Hide();
		_levelUi.SelectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var seed = _systemLoreSeeds[systemIndex];
		var systemTitles = YarnLinePool.GetPool(_lorePools, "system_titles");
		var systemDescriptions = YarnLinePool.GetPool(_lorePools, "system_descriptions");
		var title = systemTitles.Length > 0 ? Pick(systemTitles, seed) : "Unknown System";
		var description = systemDescriptions.Length > 0 ? Pick(systemDescriptions, seed) : "";
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
		if (tag != null) PostBark(tag);

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
