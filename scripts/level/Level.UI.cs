using System;
using System.Linq;
using Godot;

namespace Tts;

public partial class Level
{
	private void SelectFleet(int systemIndex)
	{
		if (_systems[systemIndex].IsPlayerOwned)
		{
			ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
			var slot = _selectedFleetSlot;
			var splitDisabled = _systems[systemIndex].GetFleetShips(slot) < 2f;
			_levelUi.SystemActionMenu.ShowForPlayerFleet(
				_systems[systemIndex].GlobalPosition,
				() => ShowFleetInfo(systemIndex),
				_rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex),
				fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
				ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge),
				splitDisabled, () => OnSplitButtonPressed(systemIndex, slot));
			ShowOwnSystemIntentPicker(systemIndex);
		}
		else if (_systems[systemIndex].IsAiOwned)
		{
			_levelUi.IntentPickerMenu.HideMenu();
			_levelUi.SystemActionMenu.ShowForAiSystem(
				_systems[systemIndex].GlobalPosition,
				() => ShowFleetInfo(systemIndex),
				() => ShowAiSystemInfo(systemIndex));
		}
		else
		{
			_levelUi.IntentPickerMenu.HideMenu();
			_levelUi.SystemActionMenu.ShowInfoOnly(_systems[systemIndex].GlobalPosition, () => ShowFleetInfo(systemIndex));
		}
	}

	private void SelectSystem(int systemIndex)
	{
		if (_systems[systemIndex].IsPlayerOwned)
		{
			ComputeUpgradeStates(systemIndex, out var fa, out var fd, out var ga, out var gd);
			var splitDisabled = !_systems[systemIndex].HasFleet || _systems[systemIndex].GetFleetShips(0) < 2f;
			_levelUi.SystemActionMenu.ShowForPlayerSystem(
				_systems[systemIndex].GlobalPosition,
				() => ShowSystemInfo(systemIndex),
				_rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex),
				fa, fd, () => DoUpgrade(systemIndex, SystemUpgrade.Fortify),
				ga, gd, () => DoUpgrade(systemIndex, SystemUpgrade.Forge),
				splitDisabled, () => OnSplitButtonPressed(systemIndex, 0));
			ShowOwnSystemIntentPicker(systemIndex);
		}
		else
		{
			_levelUi.IntentPickerMenu.HideMenu();
			_levelUi.SystemActionMenu.ShowInfoOnly(_systems[systemIndex].GlobalPosition, () => ShowSystemInfo(systemIndex));
		}
	}

	private void SelectAiSystem(int systemIndex)
	{
		_levelUi.IntentPickerMenu.HideMenu();
		_levelUi.SystemActionMenu.ShowForAiSystem(
			_systems[systemIndex].GlobalPosition,
			() => ShowSystemInfo(systemIndex),
			() => ShowAiSystemInfo(systemIndex));
	}

	private void ShowOwnSystemIntentPicker(int systemIndex)
	{
		if (!_systems[systemIndex].HasFleet)
		{
			_levelUi.IntentPickerMenu.HideMenu();
			return;
		}
		_levelUi.IntentPickerMenu.ShowAt(
			_systems[systemIndex].GlobalPosition,
			_systemRadius,
			[
				("Fortify",     IntentType.Fortify),
				("Investigate", IntentType.Investigate),
				("Exploit",     IntentType.Exploit)
			],
			intent => CommitOwnSystem(systemIndex, intent));
	}

	private void ComputeUpgradeStates(
		int systemIndex,
		out bool fortifyActive, out bool fortifyDisabled,
		out bool forgeActive, out bool forgeDisabled)
	{
		var upgrade = _systems[systemIndex].Upgrade;
		var canAfford = _systems[systemIndex].Ships >= _upgradeCfg.UpgradeCost;
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
		var pool = _systems[systemIndex].IsPlayerOwned ? _loreConfig.PlayerFleet : _loreConfig.NeutralFleet;
		var seed = _fleetLoreSeeds[systemIndex];
		var title = Pick(pool.Titles, seed);
		var description = Pick(pool.Descriptions, seed);
		_levelUi.AiSystemPanel.Hide();
		_levelUi.SelectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var seed = _systemLoreSeeds[systemIndex];
		var title = Pick(_loreConfig.System.Titles, seed);
		var description = Pick(_loreConfig.System.Descriptions, seed);
		_levelUi.AiSystemPanel.Hide();

		var scenario = _scenarioController.GetScenario(systemIndex);
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
		_scenarioController.DismissScenario(systemIndex);
		_systems[systemIndex].SetScenarioBadge(false);
		_levelUi.SelectionPanel.Hide();
	}

	private void DoUpgrade(int systemIndex, SystemUpgrade upgrade)
	{
		if (upgrade != SystemUpgrade.None)
			_systems[systemIndex].SpendShips(_upgradeCfg.UpgradeCost);
		_systems[systemIndex].ApplyUpgrade(upgrade);

		var pool = upgrade switch
		{
			SystemUpgrade.Forge   => _barkConfig?.Get("player_forge"),
			SystemUpgrade.Fortify => _barkConfig?.Get("player_fortify"),
			_ => null
		};
		PostBark(pool);

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
