using System;
using System.Linq;
using Godot;

namespace Tts;

public partial class Level
{
	private void SelectFleet(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		_infoButton.ShowFor(viewportSize, () => ShowFleetInfo(systemIndex));
		if (_systems[systemIndex].IsPlayerOwned)
		{
			_rerouteButtonNode.ShowFor(viewportSize, _rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex));
			ShowPlayerUpgradeButtons(systemIndex);
			ShowSplitButton(systemIndex, _selectedFleetSlot);
		}
		else
		{
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
			_splitButtonNode.Hide();
		}
	}

	private void SelectSystem(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		_infoButton.ShowFor(viewportSize, () => ShowSystemInfo(systemIndex));
		if (_systems[systemIndex].IsPlayerOwned)
		{
			_rerouteButtonNode.ShowFor(viewportSize, _rerouteTargets.ContainsKey(systemIndex), () => OnRerouteButtonPressed(systemIndex));
			ShowPlayerUpgradeButtons(systemIndex);
		}
		else
		{
			_rerouteButtonNode.Hide();
			_forgeButtonNode.Hide();
			_fortifyButtonNode.Hide();
		}
		_splitButtonNode.Hide();
	}

	private void SelectAiSystem(int systemIndex)
	{
		_infoButton.ShowFor(GetViewport().GetVisibleRect().Size, () => ShowAiSystemInfo(systemIndex));
		_rerouteButtonNode.Hide();
		_forgeButtonNode.Hide();
		_fortifyButtonNode.Hide();
		_splitButtonNode.Hide();
	}

	private void ShowAiSystemInfo(int systemIndex)
	{
		var owner = _systems[systemIndex].OwnerPlayer;
		var aiPlayer = _aiPlayers.FirstOrDefault(p => p.Owner == owner);
		if (aiPlayer == null) return;
		_aiColors.TryGetValue(owner, out var color);
		_selectionPanel.Hide();
		_aiSystemPanel.ShowFor(aiPlayer, color, _systemLoreSeeds[systemIndex], GetViewport().GetVisibleRect().Size);
	}

	private void ShowFleetInfo(int systemIndex)
	{
		var pool = _systems[systemIndex].IsPlayerOwned ? _loreConfig.PlayerFleet : _loreConfig.NeutralFleet;
		var seed = _fleetLoreSeeds[systemIndex];
		var title = Pick(pool.Titles, seed);
		var description = Pick(pool.Descriptions, seed);
		_aiSystemPanel.Hide();
		_selectionPanel.ShowAt($"Fleet — {title}", description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowSystemInfo(int systemIndex)
	{
		var seed = _systemLoreSeeds[systemIndex];
		var title = Pick(_loreConfig.System.Titles, seed);
		var description = Pick(_loreConfig.System.Descriptions, seed);
		_aiSystemPanel.Hide();
		_selectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowPlanetInfo(int systemIndex, int planetIndex)
	{
		var seed = _planetLoreSeeds[systemIndex][planetIndex];
		var title = Pick(_loreConfig.Planet.Titles, seed);
		var description = Pick(_loreConfig.Planet.Descriptions, seed);
		_selectionPanel.ShowAt(title, description, GetViewport().GetVisibleRect().Size);
	}

	private void ShowPlayerUpgradeButtons(int systemIndex)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		var upgrade = _systems[systemIndex].Upgrade;
		var canAfford = _systems[systemIndex].Ships >= _upgradeCfg.UpgradeCost;

		var fortifyActive = upgrade == SystemUpgrade.Fortify;
		var fortifyDisabled = !fortifyActive && (upgrade != SystemUpgrade.None || !canAfford);
		Action fortifyAction = fortifyActive
			? () => DoUpgrade(systemIndex, SystemUpgrade.None)
			: () => DoUpgrade(systemIndex, SystemUpgrade.Fortify);

		var forgeActive = upgrade == SystemUpgrade.Forge;
		var forgeDisabled = !forgeActive && (upgrade != SystemUpgrade.None || !canAfford);
		Action forgeAction = forgeActive
			? () => DoUpgrade(systemIndex, SystemUpgrade.None)
			: () => DoUpgrade(systemIndex, SystemUpgrade.Forge);

		_fortifyButtonNode.ShowFor(viewportSize, slotFromRight: 3, isActive: fortifyActive, disabled: fortifyDisabled, onPressed: fortifyAction);
		_forgeButtonNode.ShowFor(viewportSize, slotFromRight: 4, isActive: forgeActive, disabled: forgeDisabled, onPressed: forgeAction);
	}

	private void ShowSplitButton(int systemIndex, int fleetSlot)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		var ships = _systems[systemIndex].GetFleetShips(fleetSlot);
		_splitButtonNode.ShowFor(viewportSize, disabled: ships < 2f, onPressed: () => OnSplitButtonPressed(systemIndex, fleetSlot));
	}

	private void OnSplitButtonPressed(int systemIndex, int fleetSlot)
	{
		_systems[systemIndex].SplitFleet(fleetSlot);
		ShowSplitButton(systemIndex, fleetSlot);
	}

	private void DoUpgrade(int systemIndex, SystemUpgrade upgrade)
	{
		if (upgrade != SystemUpgrade.None)
			_systems[systemIndex].SpendShips(_upgradeCfg.UpgradeCost);
		_systems[systemIndex].ApplyUpgrade(upgrade);

		var pool = upgrade switch
		{
			SystemUpgrade.Forge => _barkConfig?.PlayerForge,
			SystemUpgrade.Fortify => _barkConfig?.PlayerFortify,
			_ => null
		};
		PostBark(pool);

		ShowPlayerUpgradeButtons(systemIndex);
	}

	private static string Pick(string[] pool, int seed) => pool[seed % pool.Length];
}
