using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace MixMashBannerlord148Compatibility
{
    public sealed class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "local.mixmash.bannerlord148.module-list-compatibility";
        private const string OriginalSha256 = "182BFA0A892B992B167086FDE4977E52DE4108521FE5C8F04DB060EDC4CB01FA";
        private const int TargetToken = 0x060000C1;
        private const int OldHelperToken = 0x060000CF;
        private static int _replacementCount;
        private static int _constantReplacementCount;
        private static bool _installed;
        private static MethodInfo _oldHelper;
        private static MethodInfo _oldWieldHelper;
        private static int _wieldReplacementCount;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                InstallCompatibility();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("MixMash armor compatibility could not replace the removed Bannerlord module-list call. See inner exception for details.", exception);
            }
        }

        private static void InstallCompatibility()
        {
            if (_installed) return;
            string originalPath = Path.Combine(Path.GetDirectoryName(typeof(SubModule).Assembly.Location), "MixMashChineseFantasyArmorPack.dll");
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(originalPath))
            {
                string actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!String.Equals(actualHash, OriginalSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("This compatibility shim requires the original MixMashChineseFantasyArmorPack.dll SHA256 " + OriginalSha256 + "; installed DLL is " + actualHash + ".");
            }

            Assembly armor = Assembly.LoadFrom(originalPath);
            Type targetType = armor.GetType("MixMashChineseFantasyArmorPack.SubModule", true);
            // The original protects its method bodies. Its own module initializer must decrypt them before Harmony reads IL.
            RuntimeHelpers.RunModuleConstructor(targetType.Module.ModuleHandle);
            MethodInfo target = targetType.GetMethod("OnGameStart", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new Type[] { typeof(Game), typeof(IGameStarter) }, null);
            if (target == null || target.MetadataToken != TargetToken)
                throw new InvalidOperationException("Unexpected MixMash OnGameStart method; refusing a broad patch.");
            _oldHelper = target.Module.ResolveMethod(OldHelperToken) as MethodInfo;
            if (_oldHelper == null || !_oldHelper.IsStatic || _oldHelper.GetParameters().Length != 0
                || _oldHelper.DeclaringType != targetType || _oldHelper.ReturnType != typeof(IEnumerable<ModuleInfo>))
                throw new InvalidOperationException("Unexpected MixMash module-list helper signature.");

            MethodInfo transpiler = typeof(SubModule).GetMethod("ReplaceOldModuleListCall", BindingFlags.NonPublic | BindingFlags.Static);
            _replacementCount = 0;
            _constantReplacementCount = 0;
            new Harmony(HarmonyId).Patch(target, transpiler: new HarmonyMethod(transpiler));
            if (_replacementCount != 1)
                throw new InvalidOperationException("Expected one MixMash module-list replacement, got " + _replacementCount + ".");
            if (_constantReplacementCount != 3)
                throw new InvalidOperationException("Expected three MixMash string-constant replacements, got " + _constantReplacementCount + ".");
            InstallMissionTickCompatibility(armor);
            _installed = true;
            TaleWorlds.Library.Debug.Print("MixMash armor: Bannerlord 1.4.8 compatibility applied (one module-list call, three string constants, one mission wield call).", 0,
                TaleWorlds.Library.Debug.DebugColor.White, 17592186044416UL);
        }

        private static IEnumerable<CodeInstruction> ReplaceOldModuleListCall(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo replacement = typeof(SubModule).GetMethod("GetModulesCompat", BindingFlags.Public | BindingFlags.Static);
            int matches = 0;
            foreach (CodeInstruction instruction in codes)
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && called != null && _oldHelper != null
                    && called.Module == _oldHelper.Module && called.MetadataToken == OldHelperToken)
                {
                    instruction.operand = replacement;
                    matches++;
                }
            }
            if (matches != 1)
                throw new InvalidOperationException("Expected exactly one old MixMash module-list call in OnGameStart, found " + matches + ".");
            _replacementCount = matches;
            ReplaceProtectedStringConstants(codes);
            return codes;
        }

        private static void ReplaceProtectedStringConstants(List<CodeInstruction> codes)
        {
            // Harmony moves this one method into a dynamic assembly. Its three protected string decoders
            // reject foreign callers and return null. Restore only the original three decoded literals.
            // Values independently recovered from the original initialized byte[1200] and decoder IL.
            int[] decoderTokens = new int[] { 0x06000004, 0x06000008, 0x06000007 };
            int[] encodedIds = new int[] { 1958179446, -1022230201, -1943605682 };
            string[] literals = new string[] { "SummonTroops", "SummonTroops", "config.json" };
            int[] counts = new int[3];
            for (int i = 0; i < codes.Count; i++)
            {
                MethodInfo decoder = codes[i].operand as MethodInfo;
                if (codes[i].opcode != OpCodes.Call || decoder == null || _oldHelper == null
                    || decoder.Module != _oldHelper.Module) continue;
                int slot = Array.IndexOf(decoderTokens, decoder.MetadataToken);
                if (slot < 0) continue;
                if (i < 2 || !decoder.IsGenericMethod || decoder.GetGenericArguments().Length != 1
                    || decoder.GetGenericArguments()[0] != typeof(string) || decoder.ReturnType != typeof(string)
                    || codes[i - 2].opcode != OpCodes.Ldc_I4 || !(codes[i - 2].operand is int)
                    || (int)codes[i - 2].operand != encodedIds[slot]
                    || (codes[i - 1].opcode != OpCodes.Br_S && codes[i - 1].opcode != OpCodes.Br)
                    || !(codes[i - 1].operand is Label) || !codes[i].labels.Contains((Label)codes[i - 1].operand))
                    throw new InvalidOperationException("Unexpected protected MixMash string pattern for token 0x" + decoder.MetadataToken.ToString("X8") + ".");
                // Mutate in place so every original branch label and exception-block marker survives.
                codes[i - 2].opcode = OpCodes.Ldstr;
                codes[i - 2].operand = literals[slot];
                codes[i].opcode = OpCodes.Nop;
                codes[i].operand = null;
                counts[slot]++;
            }
            if (counts[0] != 1 || counts[1] != 1 || counts[2] != 1)
                throw new InvalidOperationException("Expected exactly the three original MixMash string-constant sites; found "
                    + counts[0] + "," + counts[1] + "," + counts[2] + ".");
            _constantReplacementCount = counts[0] + counts[1] + counts[2];
        }
        private static void InstallMissionTickCompatibility(Assembly armor)
        {
            Type missionType = armor.GetType("MixMashChineseFantasyArmorPack.CompaignMissionBehavior", true);
            MethodInfo target = missionType.GetMethod("OnMissionTick", BindingFlags.Public | BindingFlags.Instance,
                null, new Type[] { typeof(float) }, null);
            if (target == null || target.MetadataToken != 0x06000073)
                throw new InvalidOperationException("Unexpected MixMash OnMissionTick target.");
            _oldWieldHelper = armor.ManifestModule.ResolveMethod(0x06000077) as MethodInfo;
            if (_oldWieldHelper == null || !_oldWieldHelper.IsStatic || _oldWieldHelper.DeclaringType != missionType
                || _oldWieldHelper.ReturnType != typeof(EquipmentIndex))
                throw new InvalidOperationException("Unexpected MixMash wield helper.");
            ParameterInfo[] parameters = _oldWieldHelper.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(Agent)
                || parameters[1].ParameterType != typeof(Agent.HandIndex))
                throw new InvalidOperationException("Unexpected MixMash wield helper parameters.");
            _wieldReplacementCount = 0;
            MethodInfo transpiler = typeof(SubModule).GetMethod("ReplaceOldWieldCall", BindingFlags.NonPublic | BindingFlags.Static);
            new Harmony(HarmonyId).Patch(target, transpiler: new HarmonyMethod(transpiler));
            if (_wieldReplacementCount != 1)
                throw new InvalidOperationException("Expected one MixMash mission wield replacement, got " + _wieldReplacementCount + ".");
        }

        private static IEnumerable<CodeInstruction> ReplaceOldWieldCall(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int matches = 0;
            MethodInfo replacement = typeof(SubModule).GetMethod("GetWieldedItemIndexCompat", BindingFlags.Public | BindingFlags.Static);
            foreach (CodeInstruction instruction in codes)
            {
                MethodInfo called = instruction.operand as MethodInfo;
                if (instruction.opcode == OpCodes.Call && called != null && _oldWieldHelper != null
                    && called.Module == _oldWieldHelper.Module && called.MetadataToken == _oldWieldHelper.MetadataToken)
                {
                    instruction.operand = replacement;
                    matches++;
                }
            }
            if (matches != 1)
                throw new InvalidOperationException("Expected exactly one removed wield-helper call in MixMash OnMissionTick, found " + matches + ".");
            // The complete original OnMissionTick has no protected constant-decoder calls.
            _wieldReplacementCount = matches;
            return codes;
        }

        public static EquipmentIndex GetWieldedItemIndexCompat(Agent agent, Agent.HandIndex hand)
        {
            if (hand == Agent.HandIndex.MainHand) return agent.GetPrimaryWieldedItemIndex();
            if (hand == Agent.HandIndex.OffHand) return agent.GetOffhandWieldedItemIndex();
            throw new ArgumentOutOfRangeException("hand", hand, "Expected a main-hand or offhand equipment slot.");
        }
        public static IEnumerable<ModuleInfo> GetModulesCompat()
        {
            return ModuleHelper.GetActiveModules();
        }
    }
}
