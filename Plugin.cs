using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace StartBoost;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "maxenterme.StartBoost";
    private const string PluginName = "StartBoost";
    private const string PluginVersion = "2.0.4";

    internal static Plugin Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;

    // Level override
    internal static ConfigEntry<int> StartLevel = null!;

    // Starting currency
    internal static ConfigEntry<int> StartCurrency = null!;

    // Extra carts
    internal static ConfigEntry<int> ExtraCarts = null!;
    internal static ConfigEntry<bool> UseSmallCarts = null!;

    // Inventory
    internal static ConfigEntry<string> InventoryItem1 = null!;
    internal static ConfigEntry<string> InventoryItem2 = null!;
    internal static ConfigEntry<string> InventoryItem3 = null!;

    // Battery
    internal static ConfigEntry<int> ExtraBatteries = null!;

    // Upgrades
    internal static ConfigEntry<int> UpgradeHealth = null!;
    internal static ConfigEntry<int> UpgradeStamina = null!;
    internal static ConfigEntry<int> UpgradeSpeed = null!;
    internal static ConfigEntry<int> UpgradeStrength = null!;
    internal static ConfigEntry<int> UpgradeRange = null!;
    internal static ConfigEntry<int> UpgradeExtraJump = null!;
    internal static ConfigEntry<int> UpgradeLaunch = null!;
    internal static ConfigEntry<int> UpgradeCrouchRest = null!;
    internal static ConfigEntry<int> UpgradeWings = null!;
    internal static ConfigEntry<int> UpgradeThrow = null!;
    internal static ConfigEntry<int> UpgradeTumbleClimb = null!;
    internal static ConfigEntry<int> UpgradeMapPlayerCount = null!;
    internal static ConfigEntry<int> UpgradeDeathHeadBattery = null!;

    private void Awake()
    {
        Instance = this;

        StartLevel = Config.Bind("Level", "StartLevel", 0,
            new ConfigDescription(
                "Override the starting level (0 = no override, 1-50 = start at that level). " +
                "Sets levelsCompleted so the game treats you as if you reached that level.",
                new AcceptableValueRange<int>(0, 50)));

        StartCurrency = Config.Bind("Economy", "StartCurrency", 0,
            new ConfigDescription(
                "Extra starting currency added at the beginning of a run (0 = no extra).",
                new AcceptableValueRange<int>(0, 100000)));

        ExtraCarts = Config.Bind("Items", "ExtraCarts", 0,
            new ConfigDescription(
                "Number of extra carts to spawn at the start of each level.",
                new AcceptableValueRange<int>(0, 5)));

        UseSmallCarts = Config.Bind("Items", "UseSmallCarts", false,
            "Use small (pocket) carts instead of medium carts.");

        InventoryItem1 = Config.Bind("Inventory", "Slot1", "",
            "Item asset name for inventory slot 1 (e.g. 'Item Gun', 'Item Tracker'). Empty = none. Only given on first join.");
        InventoryItem2 = Config.Bind("Inventory", "Slot2", "",
            "Item asset name for inventory slot 2. Empty = none. Only given on first join.");
        InventoryItem3 = Config.Bind("Inventory", "Slot3", "",
            "Item asset name for inventory slot 3. Empty = none. Only given on first join.");

        ExtraBatteries = Config.Bind("Items", "ExtraBatteries", 0,
            new ConfigDescription(
                "Extra power crystals added at run start (determines charging station capacity).",
                new AcceptableValueRange<int>(0, 20)));

        UpgradeHealth = Config.Bind("Upgrades", "Health", 0,
            new ConfigDescription("Starting Health upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeStamina = Config.Bind("Upgrades", "Stamina", 0,
            new ConfigDescription("Starting Stamina upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeSpeed = Config.Bind("Upgrades", "Speed", 0,
            new ConfigDescription("Starting Speed upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeStrength = Config.Bind("Upgrades", "Strength", 0,
            new ConfigDescription("Starting Strength upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeRange = Config.Bind("Upgrades", "Range", 0,
            new ConfigDescription("Starting Range upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeExtraJump = Config.Bind("Upgrades", "ExtraJump", 0,
            new ConfigDescription("Starting ExtraJump upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeLaunch = Config.Bind("Upgrades", "Launch", 0,
            new ConfigDescription("Starting Launch upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeCrouchRest = Config.Bind("Upgrades", "CrouchRest", 0,
            new ConfigDescription("Starting Crouch Rest upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeWings = Config.Bind("Upgrades", "Wings", 0,
            new ConfigDescription("Starting Tumble Wings upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeThrow = Config.Bind("Upgrades", "Throw", 0,
            new ConfigDescription("Starting Throw Strength upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeTumbleClimb = Config.Bind("Upgrades", "TumbleClimb", 0,
            new ConfigDescription("Starting Tumble Climb upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeMapPlayerCount = Config.Bind("Upgrades", "MapPlayerCount", 0,
            new ConfigDescription("Starting Map Player Count upgrade level", new AcceptableValueRange<int>(0, 100)));
        UpgradeDeathHeadBattery = Config.Bind("Upgrades", "DeathHeadBattery", 0,
            new ConfigDescription("Starting Death Head Battery upgrade level", new AcceptableValueRange<int>(0, 100)));

        new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded!");
    }
}
