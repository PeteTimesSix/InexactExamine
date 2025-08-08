using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.InexactExamine
{
    [HarmonyPatch(typeof(GenLabel), nameof(GenLabel.LabelExtras))]
    public static class GenLabel_Patches
    {
        static List<float> thresholds = new List<float>()
        {
            0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 0.95f
        };

        private static Dictionary<float, string> _translationsThingCached;
        static Dictionary<float, string> TranslationsThingCached
        {
            get
            {
                if (_translationsThingCached == null)
                    InitTranslationDicts();
                return _translationsThingCached;
            }
        }
        private static Dictionary<float, string> _translationsApparelCached;
        static Dictionary<float, string> TranslationsApparelCached
        {
            get
            {
                if (_translationsApparelCached == null)
                    InitTranslationDicts();
                return _translationsApparelCached;
            }
        }

        public static void InitTranslationDicts() 
        {
            _translationsThingCached = new Dictionary<float, string>();
            _translationsApparelCached = new Dictionary<float, string>();

            float previousThreshold = 0;
            foreach (var threshold in thresholds)
            {
                _translationsThingCached[threshold] = $"InexactExamine_Damage_Thing_{Math.Round(previousThreshold * 100)}to{Math.Round(threshold * 100)}".TranslateSimple();
                _translationsApparelCached[threshold] = $"InexactExamine_Damage_Apparel_{Math.Round(previousThreshold * 100)}to{Math.Round(threshold * 100)}".TranslateSimple();
                previousThreshold = threshold;
            }
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GenLabel_LabelExtras_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            var toMatch_storeMaxHp = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MaxHitPoints))),
                new CodeMatch(OpCodes.Stloc_S),
            };

            LocalBuilder maxHPlocal;
            codeMatcher.MatchStartForward(toMatch_storeMaxHp);
            if (codeMatcher.IsInvalid)
            {
                Log.Warning("InexactExamine: failed to apply transpiler (stage 1).");
                return instructions;
            }
            else
            {
                maxHPlocal = codeMatcher.InstructionAt(2).operand as LocalBuilder;
                if(maxHPlocal == null)
                {
                    Log.Warning("InexactExamine: failed to apply transpiler (stage 1) - operand was not a LocalBuilder: "+ codeMatcher.InstructionAt(2).operand?.ToString() ?? "NULL");
                    return instructions;
                }
            }

            //the first occurence of concatting " " is the one we want. There's another one later though that'd also match.
            var toMatch_space = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Ldstr, " "),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(String), nameof(String.Concat), new Type[] { typeof(String), typeof(String) })),
            };

            var replace_space = new CodeInstruction[] {
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldloc_3),
                new CodeMatch(OpCodes.Ldloc_S, maxHPlocal),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GenLabel_Patches), nameof(AddSpaceIfNeeded))),
            };

            codeMatcher.MatchStartForward(toMatch_space);
            if (codeMatcher.IsInvalid)
            {
                Log.Warning("InexactExamine: failed to apply transpiler (stage 2).");
                return instructions;
            }
            else
            {
                codeMatcher.RemoveInstructions(toMatch_space.Length);
                codeMatcher.Insert(replace_space);
            }


            var toMatch_percentage = new CodeMatch[] {
                new CodeMatch(OpCodes.Ldloc_3),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(OpCodes.Ldloc_S, maxHPlocal),
                new CodeMatch(OpCodes.Conv_R4),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GenText), nameof(GenText.ToStringPercent), new Type[] { typeof(float) })),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(String), nameof(String.Concat), new Type[] { typeof(String), typeof(String) })),
            };

            var replace_percentage = new CodeInstruction[] {
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldloc_3),
                new CodeMatch(OpCodes.Ldloc_S, maxHPlocal),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GenLabel_Patches), nameof(AddApproximateHealthToString))),
            };

            codeMatcher.MatchStartForward(toMatch_percentage);

            if (codeMatcher.IsInvalid)
            {
                Log.Warning("InexactExamine: failed to apply transpiler (stage 3).");
                return instructions;
            }
            else
            {
                codeMatcher.RemoveInstructions(toMatch_percentage.Length);
                codeMatcher.Insert(replace_percentage);
            }

            return codeMatcher.InstructionEnumeration();
        }

        public static string AddApproximateHealthToString(string str, Thing thing, int hitPoints, int maxHitPoints)
        {
            float fraction = ((float)hitPoints / (float)maxHitPoints);
            for (int i = 0; i < thresholds.Count; i++)
            {
                if(fraction < thresholds[i])
                {
                    if(thing is Apparel)
                        return str + TranslationsApparelCached[thresholds[i]];
                    else
                        return str + TranslationsThingCached[thresholds[i]];
                }
            }
            return str;

            //return str + "EDITED: " + ((float)hitPoints / (float)maxHitPoints).ToStringPercent();
        }

        public static string AddSpaceIfNeeded(string str, Thing thing, int hitPoints, int maxHitPoints)
        {
            float fraction = ((float)hitPoints / (float)maxHitPoints);
            if (fraction > thresholds[thresholds.Count - 1])
                return str;
            else
                return str + ", ";
        }
    }
}
