using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace PeteTimesSix.InexactExamine
{
    public class InexactExamine_Mod : Mod
    {
        public static InexactExamine_Mod ModSingleton { get; private set; }

        public static Harmony Harmony { get; internal set; }

        public InexactExamine_Mod(ModContentPack content) : base(content)
        {
            ModSingleton = this;

            Harmony = new Harmony("PeteTimesSix.UnexactExamine");
            Harmony.PatchAll();
        }
    }


    [StaticConstructorOnStartup]
    public static class HardVacuum_PostInit
    {
        static HardVacuum_PostInit()
        {
        }
    }

}
