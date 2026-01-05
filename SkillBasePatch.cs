using HarmonyLib;
using MelonLoader;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Effects;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Shop;
using UnityEngine;

namespace SkillTree
{
    /// <summary>
    /// ADD POINTS AT LEVEL UP
    /// </summary>
    [HarmonyPatch(typeof(LevelManager), "IncreaseTier")]
    public static class LevelUp_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Core.Instance != null)
                Core.Instance.AttPoints(true); 
        }
    }

    /// <summary>
    /// CHANGE THE RANK NECESSARY TO UNLOCK
    /// </summary>
    public static class ItemUnlocker
    {
        private static readonly Dictionary<string, FullRank> TargetRanks = new Dictionary<string, FullRank>
        {
            { "moisturepreservingpot",  new FullRank(ERank.Hoodlum, 5) },
            { "ledgrowlight",           new FullRank(ERank.Hoodlum, 3) },
            { "plasticpot",             new FullRank(ERank.Street_Rat, 5) },
            { "halogengrowlight",       new FullRank(ERank.Street_Rat, 5) },
            { "suspensionrack",         new FullRank(ERank.Street_Rat, 5) },
            { "airpot",                 new FullRank(ERank.Peddler, 2) },
            { "cauldron",               new FullRank(ERank.Bagman, 3) },
            { "brickpress",             new FullRank(ERank.Bagman, 5) },
            { "dryingrack",             new FullRank(ERank.Street_Rat, 5) }
        };

        public static void UnlockSpecificItems()
        {
            var registry = Registry.Instance;
            if (registry == null) return;

            Il2CppSystem.Collections.Generic.List<ItemDefinition> allItems = registry.GetAllItems();
            if (allItems == null) return;

            int patchedCount = 0;

            foreach (var def in allItems)
            {
                if (def == null || string.IsNullOrEmpty(def.ID)) continue;

                string id = def.ID.ToLowerInvariant();

                if (TargetRanks.TryGetValue(id, out FullRank rankAlvo))
                {
                    var storable = def as StorableItemDefinition;
                    if (storable != null)
                    {
                        storable.RequiredRank = rankAlvo;

                        storable.RequiresLevelToPurchase = true;

                        patchedCount++;
                        MelonLogger.Msg($"[SkillTree Unlocker] Item {id} updated to Rank: {rankAlvo.Rank}, Tier: {rankAlvo.Tier}");
                    }
                }
            }

            MelonLogger.Msg($"[SkillTree Unlocker] Total of {patchedCount} items successfully remapped.");
        }
    }

    /// <summary>
    /// ADD ITEMS TO HARDWARE STORES 
    /// </summary>
    [HarmonyPatch(typeof(ShopInterface), "Awake")]
    public static class ShopInjectionPatch
    {
        private static List<string> itemIdsToInject = new List<string>
        {
            "moisturepreservingpot",
            "ledgrowlight",
            "plasticpot",
            "halogengrowlight",
            "suspensionrack",
            "airpot",
            "dryingrack"
        };

        [HarmonyPostfix]
        public static void Postfix(ShopInterface __instance)
        {
            if (__instance.ShopCode.ToLower().Contains("hardware") || __instance.ShopCode.ToLower().Contains("handy_hanks"))
            {
                StorableItemDefinition[] allItems = Resources.FindObjectsOfTypeAll<StorableItemDefinition>();

                foreach (string id in itemIdsToInject)
                {
                    bool alreadyExists = false;

                    foreach (var listing in __instance.Listings)
                    {
                        if (listing.Item != null && listing.Item.ID.ToLower() == id.ToLower())
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (alreadyExists)
                        continue;

                    StorableItemDefinition targetItem = System.Array.Find(allItems, x => x.ID.ToLower() == id);

                    if (targetItem != null)
                    {
                        ShopListing newListing = new ShopListing();
                        newListing.Item = targetItem;

                        if (id == "moisturepreservingpot")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 125f;
                        }
                        if (id == "ledgrowlight")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 200f;
                        }
                        if (id == "plasticpot")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 50f;
                        }
                        if (id == "halogengrowlight")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 100f;
                        }
                        if (id == "suspensionrack")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 100f;
                        }
                        if (id == "airpot")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 300f;
                        }
                        if (id == "dryingrack")
                        {
                            newListing.OverridePrice = true;
                            newListing.OverriddenPrice = 400f;
                        }

                        Traverse trv = Traverse.Create(newListing);

                        if (newListing.IsUnlimitedStock)
                            trv.Field("isUnlimitedStock").SetValue(true);
                        else if (trv.Field("_isUnlimitedStock").FieldExists())
                            trv.Field("_isUnlimitedStock").SetValue(true);

                        __instance.Listings.Add(newListing);

                        newListing.Initialize(__instance);

                        __instance.CreateListingUI(newListing);

                        //Traverse.Create(__instance).Method("CreateListingUI", new object[] { newListing }).GetValue();

                        MelonLogger.Msg($"[SkillTree Shop] Item successfully injected: {targetItem.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// FIX MOVESPEED OF EFFECTS Athletic AND Energizing
        /// </summary>
        [HarmonyPatch]
        public static class SpeedEffect_SkillHarmony_Patch
        {

            [HarmonyPatch(typeof(Athletic), "ApplyToPlayer")]
            [HarmonyPostfix]
            public static void Athletic_Apply_Postfix()
            {
                float baseWithSkill = SkillPatchStats.PlayerMovespeed.MovespeedBase;
                PlayerSingleton<PlayerMovement>.Instance.MoveSpeedMultiplier = baseWithSkill + 0.3f;
            }

            [HarmonyPatch(typeof(Energizing), "ApplyToPlayer")]
            [HarmonyPostfix]
            public static void Energizing_Apply_Postfix()
            {
                float baseWithSkill = SkillPatchStats.PlayerMovespeed.MovespeedBase;
                PlayerSingleton<PlayerMovement>.Instance.MoveSpeedMultiplier = baseWithSkill + 0.15f;
            }

            [HarmonyPatch(typeof(Athletic), "ClearFromPlayer")]
            [HarmonyPostfix]
            public static void Athletic_Clear_Postfix()
            {
                PlayerSingleton<PlayerMovement>.Instance.MoveSpeedMultiplier = SkillPatchStats.PlayerMovespeed.MovespeedBase;
            }

            [HarmonyPatch(typeof(Energizing), "ClearFromPlayer")]
            [HarmonyPostfix]
            public static void Energizing_Clear_Postfix()
            {
                PlayerSingleton<PlayerMovement>.Instance.MoveSpeedMultiplier = SkillPatchStats.PlayerMovespeed.MovespeedBase;
            }
        }
    }

    public static class QuickPackagers
    {
        public static bool Add = false;
    }

    [HarmonyPatch]
    public static class RouteExpanderPatch
    {
        [HarmonyPatch(typeof(PackagingStation), "Awake")]
        [HarmonyPostfix]
        public static void Postfix_Speed(PackagingStation __instance)
        {
            if (QuickPackagers.Add)
                __instance.PackagerEmployeeSpeedMultiplier = 2f;
        }
    }

    public static class StackCache
    {
        public static Dictionary<string, int> ItemStack = new Dictionary<string, int>();
        public static bool IsLoaded = false;

        public static void FillCache(Il2CppSystem.Collections.Generic.List<ItemDefinition> business)
        {
            if (IsLoaded) return;

            foreach (var c in business)
            {
                string key = c.name;
                if (!ItemStack.ContainsKey(key))
                    ItemStack.Add(key, c.StackLimit);
            }
            IsLoaded = true;
            MelonLogger.Msg("ItemStack Memory successfully stored!");
        }
    }

}
