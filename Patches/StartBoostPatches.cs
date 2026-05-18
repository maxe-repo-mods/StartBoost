using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace StartBoost.Patches;

[HarmonyPatch]
public static class StartBoostPatches
{
    private static bool _upgradesApplied;
    private static readonly HashSet<string> _boostedPlayers = new HashSet<string>();
    private static readonly HashSet<string> _inventoryGivenPlayers = new HashSet<string>();

    /// <summary>
    /// After ResetProgress resets levelsCompleted to 0,
    /// override it with the configured start level.
    /// Also set starting currency here since stats are reset.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), "ResetProgress")]
    static void ResetProgress_Postfix(RunManager __instance)
    {
        _upgradesApplied = false;
        _boostedPlayers.Clear();
        _inventoryGivenPlayers.Clear();

        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

        // Level override
        int startLevel = Plugin.StartLevel.Value;
        if (startLevel > 0)
        {
            int targetCompleted = startLevel - 1;
            __instance.levelsCompleted = targetCompleted;
            SemiFunc.StatSetRunLevel(targetCompleted);
            __instance.UpdateMoonLevel();
            Plugin.Logger.LogInfo($"StartBoost: levelsCompleted={targetCompleted}, moonLevel={__instance.moonLevel}");
        }

        // Starting currency — set after ResetAllStats clears it
        int extraCurrency = Plugin.StartCurrency.Value;
        if (extraCurrency > 0)
        {
            StatsManager.instance.runStats["currency"] = extraCurrency;
            Plugin.Logger.LogInfo($"StartBoost: Starting currency set to {extraCurrency}");
        }

        // Extra power crystals (charging station capacity is based on purchased crystal count)
        int extraBatteries = Plugin.ExtraBatteries.Value;
        if (extraBatteries > 0)
        {
            string crystalKey = "Item Power Crystal";
            if (!StatsManager.instance.itemsPurchased.ContainsKey(crystalKey))
                StatsManager.instance.itemsPurchased[crystalKey] = 0;
            if (!StatsManager.instance.itemsPurchasedTotal.ContainsKey(crystalKey))
                StatsManager.instance.itemsPurchasedTotal[crystalKey] = 0;

            StatsManager.instance.itemsPurchased[crystalKey] += extraBatteries;
            StatsManager.instance.itemsPurchasedTotal[crystalKey] += extraBatteries;

            // Also set chargeTotal so the station has capacity for the crystals
            int newTotal = StatsManager.instance.itemsPurchased[crystalKey] * 10;
            StatsManager.instance.runStats["chargingStationChargeTotal"] =
                Mathf.Max(StatsManager.instance.runStats.ContainsKey("chargingStationChargeTotal")
                    ? StatsManager.instance.runStats["chargingStationChargeTotal"] : 100, newTotal);

            Plugin.Logger.LogInfo($"StartBoost: Added {extraBatteries} power crystals " +
                $"(purchased: {StatsManager.instance.itemsPurchased[crystalKey]}, " +
                $"chargeTotal: {StatsManager.instance.runStats["chargingStationChargeTotal"]})");
        }

        Plugin.Logger.LogInfo("StartBoost: ResetProgress postfix done.");
    }

    /// <summary>
    /// Single unified Postfix for GameDirector.SetStart:
    /// - Apply upgrades (every level)
    /// - Spawn inventory items (first level only)
    /// - Spawn extra carts
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameDirector), "SetStart")]
    static void SetStart_Postfix()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!SemiFunc.RunIsLevel()) return;

        _upgradesApplied = true;
        GameDirector.instance.StartCoroutine(SetStartSequence());
    }

    private static IEnumerator SetStartSequence()
    {
        yield return new WaitForSeconds(2f);

        // 1. Apply upgrades
        yield return ApplyUpgradesCoroutine();

        // 2. Spawn inventory for players who haven't received it yet
        var itemsToSpawn = GetInventoryItems();
        if (itemsToSpawn.Count > 0)
        {
            yield return SpawnInventoryForNewPlayers(itemsToSpawn);
        }

        // 3. Spawn extra carts
        int extraCarts = Plugin.ExtraCarts.Value;
        if (extraCarts > 0)
            yield return SpawnCartsCoroutine(extraCarts);
    }

    private static List<string> GetInventoryItems()
    {
        var itemNames = new[]
        {
            Plugin.InventoryItem1.Value,
            Plugin.InventoryItem2.Value,
            Plugin.InventoryItem3.Value,
        };

        var stats = StatsManager.instance;
        var itemsToSpawn = new List<string>();

        foreach (string raw in itemNames)
        {
            string? itemName = raw?.Trim();
            if (string.IsNullOrEmpty(itemName)) continue;

            if (!stats.itemDictionary.ContainsKey(itemName))
            {
                Plugin.Logger.LogError($"StartBoost: Inventory item '{itemName}' not found in itemDictionary.");
                continue;
            }

            itemsToSpawn.Add(itemName);
        }

        return itemsToSpawn;
    }

    private static IEnumerator SpawnInventoryForNewPlayers(List<string> itemNames)
    {
        var stats = StatsManager.instance;
        var playerList = GameDirector.instance.PlayerList;

        // Find players who haven't received inventory yet
        // Mark them immediately to prevent race with ApplyLateJoinBoost
        var newPlayers = new List<PlayerAvatar>();
        foreach (var player in playerList)
        {
            if (!string.IsNullOrEmpty(player.steamID) && _inventoryGivenPlayers.Add(player.steamID))
                newPlayers.Add(player);
        }

        if (newPlayers.Count == 0) yield break;

        bool needsSync = false;

        // Spawn and equip for each new player
        foreach (var player in newPlayers)
        {
            int viewID = SemiFunc.IsMultiplayer()
                ? player.physGrabber.photonView.ViewID
                : -1;
            Vector3 spawnPos = player.transform.position;
            int nextSlot = 0;

            for (int i = 0; i < itemNames.Count; i++)
            {
                string itemName = itemNames[i];
                var itemData = stats.itemDictionary[itemName];
                string resourcePath = itemData.prefab.ResourcePath;

                if (string.IsNullOrEmpty(resourcePath))
                {
                    Plugin.Logger.LogError($"StartBoost: No prefab for '{itemName}'");
                    continue;
                }

                Vector3 pos = spawnPos + new Vector3(0f, 2f + 1.5f * i, 0f);
                GameObject? obj = null;

                try
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        obj = PhotonNetwork.InstantiateRoomObject(resourcePath, pos, Quaternion.identity);
                    }
                    else
                    {
                        var prefab = Resources.Load<GameObject>(resourcePath);
                        if (prefab != null)
                            obj = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError($"StartBoost: Failed to spawn '{itemName}': {e.Message}");
                }

                if (obj == null) continue;

                // Register for persistence only after successful spawn
                if (!stats.itemsPurchased.ContainsKey(itemName))
                    stats.itemsPurchased[itemName] = 0;
                if (!stats.itemsPurchasedTotal.ContainsKey(itemName))
                    stats.itemsPurchasedTotal[itemName] = 0;
                stats.itemsPurchased[itemName]++;
                stats.itemsPurchasedTotal[itemName]++;
                stats.itemDictionary[itemName].maxAmountInShop++;
                stats.itemDictionary[itemName].maxAmount = Mathf.Max(
                    stats.itemDictionary[itemName].maxAmount,
                    stats.itemsPurchased[itemName]);
                needsSync = true;

                var itemAttr = obj.GetComponent<ItemAttributes>();
                float timeout = 10f;
                while (itemAttr != null && string.IsNullOrEmpty(itemAttr.instanceName) && timeout > 0f)
                {
                    yield return new WaitForSeconds(0.2f);
                    timeout -= 0.2f;
                }

                var equippable = obj.GetComponent<ItemEquippable>();
                if (equippable != null)
                {
                    equippable.RequestEquip(nextSlot, viewID);
                    Plugin.Logger.LogInfo($"StartBoost: Equipped '{itemName}' to player {player.steamID} slot {nextSlot}");
                    nextSlot++;
                }

                yield return new WaitForSeconds(0.3f);
            }

            yield return new WaitForSeconds(0.5f);
        }

        if (needsSync && SemiFunc.IsMultiplayer())
            PunManager.instance.SyncAllDictionaries();
    }


    private static IEnumerator SpawnCartsCoroutine(int count)
    {
        string cartAssetName = Plugin.UseSmallCarts.Value ? "Item Cart Small" : "Item Cart Medium";
        string? resourcePath = null;

        // Get resource path from itemDictionary
        if (StatsManager.instance.itemDictionary.TryGetValue(cartAssetName, out var cartItem))
        {
            resourcePath = cartItem.prefab.ResourcePath;
        }

        if (string.IsNullOrEmpty(resourcePath))
        {
            Plugin.Logger.LogError($"StartBoost: Cart '{cartAssetName}' not found in itemDictionary");
            yield break;
        }

        // Spawn carts above the first player's position — they'll drop down via physics
        var firstPlayer = GameDirector.instance.PlayerList.Count > 0
            ? GameDirector.instance.PlayerList[0] : null;
        Vector3 spawnPos = firstPlayer != null ? firstPlayer.transform.position : GetSpawnPosition();
        Plugin.Logger.LogInfo($"StartBoost: Cart spawn base position: {spawnPos}, resourcePath={resourcePath}");

        for (int i = 0; i < count; i++)
        {
            // Stack vertically above player to avoid clipping into walls
            Vector3 pos = spawnPos + new Vector3(0f, 2f + 1.5f * i, 0f);

            try
            {
                if (SemiFunc.IsMultiplayer())
                {
                    var obj = PhotonNetwork.InstantiateRoomObject(resourcePath, pos, Quaternion.identity);
                    Plugin.Logger.LogInfo($"StartBoost: Cart {i + 1}/{count} spawned (Photon) at {pos}, obj={obj?.name ?? "NULL"}");
                }
                else
                {
                    var prefab = Resources.Load<GameObject>(resourcePath);
                    if (prefab != null)
                    {
                        var obj = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                        Plugin.Logger.LogInfo($"StartBoost: Cart {i + 1}/{count} spawned (local) at {pos}, obj={obj?.name ?? "NULL"}");
                    }
                    else
                    {
                        Plugin.Logger.LogError($"StartBoost: Failed to load prefab '{resourcePath}'");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"StartBoost: Cart {i + 1}/{count} spawn failed: {e.Message}");
            }

            yield return new WaitForSeconds(0.5f);
        }

        Plugin.Logger.LogInfo($"StartBoost: Finished spawning {count} carts");
    }

    private static Vector3 GetSpawnPosition()
    {
        // Use SpawnPoint objects (level interior spawn points) for reliable in-level positioning
        var spawnPoints = UnityEngine.Object.FindObjectsOfType<SpawnPoint>();
        if (spawnPoints.Length > 0)
        {
            return spawnPoints[0].transform.position;
        }

        if (GameDirector.instance != null && GameDirector.instance.PlayerList.Count > 0)
        {
            return GameDirector.instance.PlayerList[0].transform.position;
        }

        Plugin.Logger.LogWarning("StartBoost: No spawn reference found, using Vector3.zero");
        return Vector3.zero;
    }

    /// <summary>
    /// Apply upgrades + inventory to late joiners.
    /// StatsManager.PlayerAdd is called for every player (including late join).
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StatsManager), "PlayerAdd")]
    static void PlayerAdd_Postfix(string _steamID, string _playerName)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
        if (!SemiFunc.RunIsLevel()) return;
        if (string.IsNullOrEmpty(_steamID)) return;
        if (_boostedPlayers.Contains(_steamID)) return;

        // Only trigger for late joiners (after initial SetStart has already run)
        if (!_upgradesApplied) return;

        _boostedPlayers.Add(_steamID);
        GameDirector.instance.StartCoroutine(ApplyLateJoinBoost(_steamID));
    }

    private static IEnumerator ApplyLateJoinBoost(string steamID)
    {
        // Wait until game is fully in Main phase (past truck preparation)
        float timeout = 30f;
        while (GameDirector.instance.currentState != GameDirector.gameState.Main && timeout > 0f)
        {
            yield return new WaitForSeconds(1f);
            timeout -= 1f;
        }
        if (timeout <= 0f)
        {
            Plugin.Logger.LogWarning($"StartBoost (LateJoin): Timed out waiting for Main state for {steamID}");
            yield break;
        }
        yield return new WaitForSeconds(2f);

        // Upgrades
        var pun = PunManager.instance;
        if (pun != null)
        {
            foreach (var (rpcName, getValue) in UpgradeEntries)
            {
                int target = getValue();
                if (target <= 0) continue;

                string dictName = "playerUpgrade" + rpcName;
                var dict = StatsManager.instance.dictionaryOfDictionaries[dictName];
                int current = dict.ContainsKey(steamID) ? dict[steamID] : 0;
                int delta = target - current;
                if (delta <= 0) continue;

                pun.photonView.RPC("TesterUpgradeCommandRPC", RpcTarget.All,
                    new object[] { steamID, rpcName, delta });
                Plugin.Logger.LogInfo($"StartBoost (LateJoin): {steamID} {rpcName} +{delta}");
            }
        }

        // Inventory for first-time joiners
        if (!_inventoryGivenPlayers.Contains(steamID))
        {
            var itemsToSpawn = GetInventoryItems();
            if (itemsToSpawn.Count > 0)
            {
                // Find the PlayerAvatar for this steamID
                PlayerAvatar? latePlayer = null;
                foreach (var p in GameDirector.instance.PlayerList)
                {
                    if (p.steamID == steamID)
                    {
                        latePlayer = p;
                        break;
                    }
                }
                if (latePlayer != null)
                {
                    _inventoryGivenPlayers.Add(steamID);
                    yield return SpawnInventoryForPlayer(latePlayer, itemsToSpawn);
                }
            }
        }
    }

    private static IEnumerator SpawnInventoryForPlayer(PlayerAvatar player, List<string> itemNames)
    {
        var stats = StatsManager.instance;
        bool needsSync = false;

        int viewID = SemiFunc.IsMultiplayer()
            ? player.physGrabber.photonView.ViewID
            : -1;
        Vector3 spawnPos = player.transform.position;
        int nextSlot = 0;

        for (int i = 0; i < itemNames.Count; i++)
        {
            string itemName = itemNames[i];
            string resourcePath = stats.itemDictionary[itemName].prefab.ResourcePath;
            if (string.IsNullOrEmpty(resourcePath)) continue;

            Vector3 pos = spawnPos + new Vector3(0f, 2f + 1.5f * i, 0f);
            GameObject? obj = null;

            try
            {
                if (SemiFunc.IsMultiplayer())
                    obj = PhotonNetwork.InstantiateRoomObject(resourcePath, pos, Quaternion.identity);
                else
                {
                    var prefab = Resources.Load<GameObject>(resourcePath);
                    if (prefab != null)
                        obj = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                }
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"StartBoost: Failed to spawn '{itemName}' for late joiner: {e.Message}");
            }

            if (obj == null) continue;

            // Register for persistence only after successful spawn
            if (!stats.itemsPurchased.ContainsKey(itemName))
                stats.itemsPurchased[itemName] = 0;
            if (!stats.itemsPurchasedTotal.ContainsKey(itemName))
                stats.itemsPurchasedTotal[itemName] = 0;
            stats.itemsPurchased[itemName]++;
            stats.itemsPurchasedTotal[itemName]++;
            stats.itemDictionary[itemName].maxAmountInShop++;
            stats.itemDictionary[itemName].maxAmount = Mathf.Max(
                stats.itemDictionary[itemName].maxAmount,
                stats.itemsPurchased[itemName]);
            needsSync = true;

            var itemAttr = obj.GetComponent<ItemAttributes>();
            float timeout = 10f;
            while (itemAttr != null && string.IsNullOrEmpty(itemAttr.instanceName) && timeout > 0f)
            {
                yield return new WaitForSeconds(0.2f);
                timeout -= 0.2f;
            }

            var equippable = obj.GetComponent<ItemEquippable>();
            if (equippable != null)
            {
                equippable.RequestEquip(nextSlot, viewID);
                Plugin.Logger.LogInfo($"StartBoost: Equipped '{itemName}' to late joiner {player.steamID} slot {nextSlot}");
                nextSlot++;
            }

            yield return new WaitForSeconds(0.3f);
        }

        if (needsSync && SemiFunc.IsMultiplayer())
            PunManager.instance.SyncAllDictionaries();
    }

    // TesterUpgradeCommandRPC upgrade names (must match game's switch cases)
    private static readonly (string rpcName, Func<int> getValue)[] UpgradeEntries =
    {
        ("Health",           () => Plugin.UpgradeHealth.Value),
        ("Stamina",          () => Plugin.UpgradeStamina.Value),
        ("Speed",            () => Plugin.UpgradeSpeed.Value),
        ("Strength",         () => Plugin.UpgradeStrength.Value),
        ("Range",            () => Plugin.UpgradeRange.Value),
        ("ExtraJump",        () => Plugin.UpgradeExtraJump.Value),
        ("Launch",           () => Plugin.UpgradeLaunch.Value),
        ("CrouchRest",       () => Plugin.UpgradeCrouchRest.Value),
        ("TumbleWings",      () => Plugin.UpgradeWings.Value),
        ("Throw",            () => Plugin.UpgradeThrow.Value),
        ("TumbleClimb",      () => Plugin.UpgradeTumbleClimb.Value),
        ("MapPlayerCount",   () => Plugin.UpgradeMapPlayerCount.Value),
        ("DeathHeadBattery", () => Plugin.UpgradeDeathHeadBattery.Value),
    };

    private static IEnumerator ApplyUpgradesCoroutine()
    {
        var pun = PunManager.instance;
        if (pun == null) yield break;

        foreach (var player in GameDirector.instance.PlayerList)
        {
            string steamID = player.steamID;
            if (string.IsNullOrEmpty(steamID)) continue;

            _boostedPlayers.Add(steamID);

            foreach (var (rpcName, getValue) in UpgradeEntries)
            {
                int target = getValue();
                if (target <= 0) continue;

                // Get current level to compute delta
                string dictName = "playerUpgrade" + rpcName;
                var dict = StatsManager.instance.dictionaryOfDictionaries[dictName];
                int current = dict.ContainsKey(steamID) ? dict[steamID] : 0;
                int delta = target - current;
                if (delta <= 0) continue;

                // Use TesterUpgradeCommandRPC to sync to all clients
                if (SemiFunc.IsMultiplayer())
                {
                    pun.photonView.RPC("TesterUpgradeCommandRPC", RpcTarget.All,
                        new object[] { steamID, rpcName, delta });
                }
                else
                {
                    pun.TesterUpgradeCommandRPC(steamID, rpcName, delta);
                }

                Plugin.Logger.LogInfo($"StartBoost: {steamID} {rpcName} +{delta} (now {target})");
            }
        }
    }
}
