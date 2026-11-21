using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SafeRefinement;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "rkmax.saferefinement";
    public const string Name = "SafeRefinement";
    public const string Version = "1.0.0";

    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> FailureEffect;
    internal static ManualLogSource Log;

    private static Plugin _instance;
    private static DateTime _configWrite;
    private Harmony _harmony;

    private void Awake()
    {
        _instance = this;
        Log = Logger;
        Enabled = Config.Bind("General", "Enabled", true,
            "A failed refinement at the Forge of Potential keeps the item at its level instead of destroying it. " +
            "Odds and costs stay vanilla: 65% success, and the idol and materials are used up either way.");
        FailureEffect = Config.Bind("General", "FailureEffect", false,
            "Play the Forge's failure effect when a refinement fails. Only used while Enabled is true.");
        _configWrite = ConfigWriteTime();

        _harmony = new Harmony(Guid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
    }

    private void Update()
    {
        DateTime write = ConfigWriteTime();
        if (write == _configWrite)
            return;
        _configWrite = write;
        Config.Reload();
        Log.LogInfo("Config reloaded");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    private static DateTime ConfigWriteTime()
    {
        string path = _instance.Config.ConfigFilePath;
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
    }
}

/// <summary>
/// InventoryGui.DoCrafting rolls a refinement: success if the idol's m_upgradeChance (0.65) beats the roll,
/// else the item breaks if m_breakChance (1 on every idol) >= 1 - roll, else it comes back at level - 1.
/// With Enabled the break check sees 0, so a failure takes the "fail" branch, and that branch gives the
/// item back at its current level instead of one lower, with its own message and no failure effect.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
internal static class DoCraftingPatch
{
    private const string FailMessageKey = "$msg_upgrader_failed";
    private const string OwnKey = "rkmax_saferefinement_fail";
    private const string OwnText = "Refining of $1 failed, it stays at level $2";

    private static readonly FieldInfo BreakChanceField =
        AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_breakChance));
    private static readonly FieldInfo FailEffectsField =
        AccessTools.Field(typeof(CraftingStation), nameof(CraftingStation.m_craftItemDoneFailEffects));
    private static readonly MethodInfo AddItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
        new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string),
                typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool) });
    private static readonly FieldInfo Translations = AccessTools.Field(typeof(Localization), "m_translations");

    private static readonly EffectList NoEffects = new EffectList();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var original = new List<CodeInstruction>(code);

        int breakReads = 0, failMessages = 0, levelDrops = 0, failEffects = 0;
        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].LoadsField(BreakChanceField))
            {
                breakReads++;
                code.Insert(++i, CodeInstruction.Call(typeof(DoCraftingPatch), nameof(BreakChance)));
            }
            else if (code[i].LoadsField(FailEffectsField))
            {
                failEffects++;
                code.Insert(++i, CodeInstruction.Call(typeof(DoCraftingPatch), nameof(FailEffects)));
            }
            else if (code[i].opcode == OpCodes.Ldstr && code[i].operand as string == FailMessageKey)
            {
                failMessages++;
                code.Insert(++i, CodeInstruction.Call(typeof(DoCraftingPatch), nameof(FailMessage)));
                // The fail branch computes "level - 2" twice (for the message and for AddItem) before
                // its AddItem call: the item's new quality, one below the current one.
                for (int j = i + 1; j < code.Count && !code[j].Calls(AddItem); j++)
                {
                    if (code[j].opcode == OpCodes.Ldc_I4_2 && code[j + 1].opcode == OpCodes.Sub)
                    {
                        levelDrops++;
                        code.Insert(++j, CodeInstruction.Call(typeof(DoCraftingPatch), nameof(FailDrop)));
                    }
                }
            }
        }

        if (breakReads != 1 || failMessages != 1 || levelDrops != 2 || failEffects != 1)
        {
            Plugin.Log.LogError("InventoryGui.DoCrafting is not the code this mod expects " +
                $"(break reads {breakReads}/1, fail messages {failMessages}/1, level drops {levelDrops}/2, " +
                $"fail effects {failEffects}/1). Not patching: refinement stays vanilla and can break items.");
            return original;
        }
        Plugin.Log.LogInfo("Patched InventoryGui.DoCrafting: failed refinements keep the item");
        return code;
    }

    private static float BreakChance(float vanilla) => Plugin.Enabled.Value ? 0f : vanilla;

    // "level - 2" in vanilla's fail branch; "level - 1" (the item's current quality) while enabled.
    private static int FailDrop(int vanilla) => Plugin.Enabled.Value ? 1 : vanilla;

    private static string FailMessage(string vanilla)
    {
        if (!Plugin.Enabled.Value)
            return vanilla;
        // Localize turns "$1" into "[1]" unless the text comes from a translation, so register one.
        if (Translations?.GetValue(Localization.instance) is Dictionary<string, string> words && !words.ContainsKey(OwnKey))
            words[OwnKey] = OwnText;
        return "$" + OwnKey;
    }

    private static EffectList FailEffects(EffectList vanilla) =>
        Plugin.Enabled.Value && !Plugin.FailureEffect.Value ? NoEffects : vanilla;
}
