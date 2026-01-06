using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.Variables;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using static Il2CppScheduleOne.ObjectScripts.Pot;
using static MelonLoader.MelonLogger;

namespace SkillTree.SkillPatchOperations
{
    /// <summary>
    /// ABSORBENT SOIL
    /// </summary>
    public static class AbsorbentSoil
    {
        public static bool Add = false;
    }


    [HarmonyPatch(typeof(Pot), "OnPlantFullyHarvested")]
    public static class Pot_OnPlantFullyHarvested_Patch
    {
        static bool Prefix(Pot __instance)
        {
            if (!AbsorbentSoil.Add)
                return true; 
            try
            {
                //var traverse = Traverse.Create(__instance);

                var plant = __instance.Plant;
                if (plant == null)
                {
                    MelonLogger.Msg("OnPlantFullyHarvested skipped: Plant is null");
                    return false;
                }

                if (InstanceFinder.IsServer)
                {
                    float value = NetworkSingleton<VariableDatabase>.Instance
                        .GetValue<float>("HarvestedPlantCount");

                    NetworkSingleton<VariableDatabase>.Instance
                        .SetVariableValue("HarvestedPlantCount", (value + 1f).ToString());

                    NetworkSingleton<LevelManager>.Instance.AddXP(5);

                    MelonLogger.Msg("Server harvest processed");
                }

                //traverse.Property("Plant")?.SetValue(null);

                int remainingUses = __instance._remainingSoilUses - 1;
                __instance.SetRemainingSoilUses(remainingUses);

                __instance.SetSoilState(ESoilState.Flat);

                if (remainingUses <= 0)
                {
                    MelonLogger.Msg("Soil depleted: clearing soil and additives");

                    __instance.ClearAdditives();
                    __instance.ClearSoil();
                }
                else
                {
                    MelonLogger.Msg("Soil still usable: additives preserved");
                }

                return false; 
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"OnPlantFullyHarvested patch failed: {ex}");
                return true; 
            }
        }
    }

    /// <summary>
    /// INCREASE CAULDRON OUTPUT
    /// </summary>
    public static class CauldronOutputAdd
    {
        public static int Add = 10;
    }

    [HarmonyPatch(typeof(Cauldron), "RpcLogic___FinishCookOperation_2166136261")]
    public static class Cauldron_Finish_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Cauldron __instance)
        {
        }
    }

    [HarmonyPatch(typeof(QualityItemDefinition), "GetDefaultInstance", typeof(int))]
    public static class Cauldron_Double_Output_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(QualityItemDefinition __instance, ref int quantity)
        {
            if (CauldronOutputAdd.Add == 10) return;

            if (__instance.name.Contains("CocaineBase") && quantity == 10)
            {
                quantity = CauldronOutputAdd.Add; 
            }
        }
    }

    /// <summary>
    /// SPEED UP CHEMIST STATIONS
    /// </summary>
    public static class StationTimeLess
    {
        public static float TimeAjust = 1f;
    }

    [HarmonyPatch(typeof(Cauldron), "MinPass")]
    public static class Cauldron_Speed_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Cauldron __instance)
        {
            if (__instance.RemainingCookTime > 0)
            {
                if (StationTimeLess.TimeAjust > 1f)
                    __instance.RemainingCookTime--;
            }
        }
    }

    [HarmonyPatch(typeof(ChemistryStation), "MinPass")]
    public static class ChemistryStation_MinPass_IL2CPP_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ChemistryStation __instance)
        {
            if (StationTimeLess.TimeAjust > 1f && __instance.CurrentCookOperation != null)
            {
                __instance.CurrentCookOperation.Progress(1);
            }
        }
    }

    [HarmonyPatch(typeof(LabOven), "MinPass")]
    public static class Oven_FastProgress_IL2CPP_Patch
    {
        [HarmonyPostfix]
        public static void Prefix(LabOven __instance)
        {
            if (StationTimeLess.TimeAjust > 1f && __instance.CurrentOperation != null)
            {
                __instance.CurrentOperation.CookProgress++;
            }
        }
    }

    /// <summary>
    /// INCREASE MIXSTATION OUTPUT AND FIXS
    /// </summary>
    public static class MixOutputAdd
    {
        public static int Add = 1;
        public static int TimeAjust = 1;
    }

    [HarmonyPatch(typeof(MixingStation))]
    public static class MixStationPatch
    {
        [HarmonyPatch("GetMixQuantity")]
        [HarmonyPostfix]
        public static void Postfix(MixingStation __instance, ref int __result)
        {
            if (__result <= 0) return;

            if (__instance.ProductSlot == null || __instance.MixerSlot == null) return;

            int qtyProduct = __instance.ProductSlot.Quantity;
            int qtyMixer = __instance.MixerSlot.Quantity;
            int originalMax = Mathf.Min(Mathf.Min(qtyProduct, qtyMixer), __instance.MaxMixQuantity * MixOutputAdd.Add);

            __result = originalMax;
        }
    }

    [HarmonyPatch(typeof(MixingStation), "MinPass")]
    public static class MixStation_Time_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(MixingStation __instance)
        {
            if (__instance == null) return;

            if (__instance.CurrentMixTime < __instance.GetMixTimeForCurrentOperation() / 2)
            {
                if (MixOutputAdd.TimeAjust > 1)
                    __instance.CurrentMixTime = (int)(__instance.GetMixTimeForCurrentOperation() / 2);
                if (MixOutputAdd.Add == 2)
                    __instance.CurrentMixTime += (int)(__instance.GetMixTimeForCurrentOperation() / 4);
            }
        }
    }

    public static class StackItem2xFix
    {
        private static bool _add = false;
        public static bool Add
        {
            get => _add;
            set
            {
                if (_add != value)
                {
                    _add = value;
                    UpdateAllRacks();
                }
            }
        }

        public static void UpdateAllRacks()
        {
            DryingRack[] racks = GameObject.FindObjectsOfType<DryingRack>();
            foreach (var rack in racks)
            {
                DryingRack_Patch.ApplyCapacityUpdate(rack);
            }
            MelonLogger.Msg($"[DryingRack] Capacity updated for all active {racks.Length} racks.");
        }
    }

    [HarmonyPatch(typeof(DryingRack), "InitializeGridItem")]
    public static class DryingRack_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(DryingRack __instance)
        {
            ApplyCapacityUpdate(__instance);
        }

        public static void ApplyCapacityUpdate(DryingRack __instance)
        {
            int targetCapacity = StackItem2xFix.Add ? 40 : 20;

            __instance.ItemCapacity = targetCapacity;

            if (__instance.HangAlignments != null && __instance.HangAlignments.Length != targetCapacity)
            {

                Transform[] originalTransforms = __instance.GetComponentsInChildren<Transform>();

                Transform[] newAlignments = new Transform[targetCapacity];

                for (int i = 0; i < targetCapacity; i++)
                {
                    newAlignments[i] = __instance.HangAlignments[i % __instance.HangAlignments.Length];
                }
                __instance.HangAlignments = newAlignments;
            }

            //FieldInfo hangSlotsField = AccessTools.Field(typeof(DryingRack), "hangSlots");
            Array currentHangSlots = (Array)__instance.hangSlots;

            if (currentHangSlots != null && currentHangSlots.Length != targetCapacity)
            {
                Type elementType = currentHangSlots.GetType().GetElementType();
                Array newHangSlots = Array.CreateInstance(elementType, targetCapacity);

                int itemsToCopy = Math.Min(currentHangSlots.Length, targetCapacity);
                Array.Copy(currentHangSlots, newHangSlots, itemsToCopy);

                if (targetCapacity > currentHangSlots.Length)
                {
                    for (int i = currentHangSlots.Length; i < targetCapacity; i++)
                    {
                        object newSlot = Activator.CreateInstance(elementType);
                        newHangSlots.SetValue(newSlot, i);
                    }
                }
                __instance.hangSlots = (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ItemSlot>)newHangSlots;
                //hangSlotsField.SetValue(__instance, newHangSlots);
            }

            __instance.RefreshHangingVisuals();
        }
    }

    /// <summary>
    /// CHANGE GROW SPEED
    /// </summary>
    public static class GrowthSpeedUp
    {
        public static float Add = 0f;
    }

    [HarmonyPatch(typeof(Plant), "MinPass")]
    public static class Plant_MinPass_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Plant __instance, int mins)
        {
            if (__instance.NormalizedGrowthProgress >= 1f || NetworkSingleton<TimeManager>.Instance.IsEndOfDay)
                return true; 

            float num = 1f / ((float)__instance.GrowthTime * 60f) * (float)mins;
            num *= __instance.Pot.GetTemperatureGrowthMultiplier();
            num *= __instance.Pot.GetAverageLightExposure(out var growSpeedMultiplier);
            num *= __instance.Pot.GrowSpeedMultiplier;
            num *= growSpeedMultiplier;
            num += (num * GrowthSpeedUp.Add);

            if (GameManager.IS_TUTORIAL)
                num *= 0.3f;

            if (__instance.Pot.NormalizedMoistureAmount <= 0f)
                num *= 0f;

            //MelonLogger.Msg($" Before Growth Plant  {__instance.NormalizedGrowthProgress}");
            //MelonLogger.Msg($" Add Growth Plant  {__instance.NormalizedGrowthProgress}");
            __instance.SetNormalizedGrowthProgress(__instance.NormalizedGrowthProgress + num);
            //MelonLogger.Msg($" After Growth Plant {__instance.NormalizedGrowthProgress}");

            return false;
        }
    }

    [HarmonyPatch(typeof(ShroomColony), "ChangeGrowthPercentage")]
    public static class MushroomGrowthSpeedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ShroomColony __instance, ref float change)
        {
            if (change > 0f && GrowthSpeedUp.Add > 0f)
            {
                MelonLogger.Msg($" Growth Shroom {__instance.GrowthProgress}");
                MelonLogger.Msg($" Before Shroom change {change}");
                change += change * GrowthSpeedUp.Add;
                MelonLogger.Msg($" After Shroom change {change}");
            }
        }
    }

    /// <summary>
    /// CHANGE QUALITY SYSTEM BY POT TYPE -- BETTER POT = BETTER QUALITY
    /// </summary>
    /// 

    public static class QualityMushroomUP
    {
        public static float Add = 0f;
    }

    [HarmonyPatch(typeof(ShroomColony), "GetHarvestedShroom")]
    public static class MushroomQualityPatch
    {
        private static readonly HashSet<int> processedIds = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(ShroomColony __instance, ref ShroomInstance __result)
        {
            if (QualityMushroomUP.Add <= 0f || __result == null) return;

            int id = __instance.GetInstanceID();
            if (processedIds.Contains(id)) return;

            float baseQuality = __instance.NormalizedQuality;
            //MelonLogger.Msg($"Base: {baseQuality}");

            __instance.ChangeQuality(QualityMushroomUP.Add);

            processedIds.Add(id);

            float boostedQuality = __instance.NormalizedQuality;
            //MelonLogger.Msg($"Boosted: {boostedQuality}");

            MelonCoroutines.Start(CleanUp(id));
        }

        private static System.Collections.IEnumerator CleanUp(int id)
        {
            yield return new WaitForSeconds(120f);
            processedIds.Remove(id);
        }
    }

    public static class QualityUP
    {
        public static float Add = 0f;
    }

    public static class BetterGrowTent
    {
        public static float Add = 0f;
    }

    [HarmonyPatch(typeof(Plant), "Initialize")]
    public static class PlantQualityPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Plant __instance)
        {
            if (__instance.Pot != null)
            {
                string potName = __instance.Pot.Name.ToString();
                float baseQuality = 0.5f;
                float currentQuality = __instance.QualityLevel;

                if (potName.Equals("Grow Tent")) baseQuality = 0.1f + BetterGrowTent.Add;
                else if (potName.Equals("Plastic Pot")) baseQuality = 0.26f;
                else if (potName.Equals("Moisture-Preserving Pot")) baseQuality = 0.36f;
                else if (potName.Equals("Air Pot")) baseQuality = 0.5f;
                else baseQuality = 0.1f; 

                float finalQuality = baseQuality + QualityUP.Add;

                ///
                if (AbsorbentSoil.Add)
                {
                    if (__instance.Pot == null)
                        MelonLogger.Warning("Plant.Initialize Postfix: Pot is null");
                    else
                    {
                        var additives = __instance.Pot.AppliedAdditives;
                        if (additives == null || additives.Count == 0)
                            MelonLogger.Msg("No initial additives found for instant growth");
                        else
                        {
                            float delta = 0f;
                            foreach (var additive in additives)
                            {
                                if (additive == null)
                                    continue;

                                MelonLogger.Msg("Additive Name: " + additive.Name.ToString().ToLower());

                                /*switch (additive.Name.ToString().ToLower().Trim())
                                {
                                    case "fertilizer":
                                        delta = +0.3f;
                                        break;

                                    case "pgr":
                                        delta = -0.3f;
                                        break;

                                    case "speedgrow":
                                        delta = -0.3f;
                                        break;
                                }*/


                                //finalQuality += delta;
                                //MelonLogger.Msg($"[SkillTree] Change Quality {finalQuality} | Additive: {additive.Name.ToString().ToLower().Trim()}");

                                if (additive.InstantGrowth > 0f && __instance.NormalizedGrowthProgress < 0.5f)
                                {
                                    float before = __instance.NormalizedGrowthProgress;

                                    __instance.SetNormalizedGrowthProgress(
                                        before + additive.InstantGrowth
                                    );

                                    MelonLogger.Msg(
                                        $"Instant growth applied: +{additive.InstantGrowth} (from {before} to {__instance.NormalizedGrowthProgress})"
                                    );
                                }

                                if (finalQuality < 0.27f && finalQuality > 0.17f)
                                    finalQuality = 0.27f;
                            }
                        }
                    }
                }

                __instance.QualityLevel = finalQuality;

              /*  var traverse = Traverse.Create(__instance);
                traverse.Field("QualityLevel").SetValue(finalQuality);

                traverse.Field("<QualityLevel>k__BackingField").SetValue(finalQuality);

                traverse.Field("_qualityLevel").SetValue(finalQuality);*/

                MelonLogger.Msg($"[SkillTree] Plant Init: {potName} | Final: {finalQuality} | Skill: {QualityUP.Add} | Total: {__instance.QualityLevel}");
            }
        }
    }

    [HarmonyPatch(typeof(Plant), "Initialize")]
    public static class Plant_Initialize_Patch
    {
        static void Postfix(Plant __instance)
        {
            try
            {
                if (!AbsorbentSoil.Add)
                    return;

                if (__instance.Pot == null)
                {
                    MelonLogger.Warning("Plant.Initialize Postfix: Pot is null");
                    return;
                }

                var additives = __instance.Pot.AppliedAdditives;
                if (additives == null || additives.Count == 0)
                {
                    MelonLogger.Msg("No initial additives found for instant growth");
                    return;
                }

                foreach (var additive in additives)
                {
                    if (additive == null)
                        continue;

                    if (additive.InstantGrowth > 0f && __instance.NormalizedGrowthProgress < 0.5f)
                    {
                        float before = __instance.NormalizedGrowthProgress;

                        __instance.SetNormalizedGrowthProgress(
                            before + additive.InstantGrowth
                        );

                        MelonLogger.Msg(
                            $"Instant growth applied: +{additive.InstantGrowth} (from {before} to {__instance.NormalizedGrowthProgress})"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Plant.Initialize Postfix failed: {ex}");
            }
        }
    }

    /// <summary>
    /// ADD YIELD FROM PLANTS
    /// </summary>
    public static class YieldAdd
    {
        public static int Add = 0;
    }

    [HarmonyPatch(typeof(Plant), "GrowthDone")]
    public static class GrowthDone_SmartBasePatch
    {
        [HarmonyPrefix]
        public static void Prefix(Plant __instance)
        {
            if (!Il2CppFishNet.InstanceFinder.IsServer) return;

            var currentMultiplier = __instance.YieldMultiplier;
            var originalBase = __instance.BaseYieldQuantity;

            //var traverse = Traverse.Create(__instance);

            if (Mathf.Approximately(currentMultiplier, 1.0f) && YieldAdd.Add != 0 && originalBase == 12)
            {
                int finalBase = originalBase + YieldAdd.Add; 

                __instance.BaseYieldQuantity = finalBase; 
                MelonLogger.Msg($"[Skill More Yield] No additives detected. Skill applied. New Base: {finalBase}");
            }
            /*else
                __instance.BaseYieldQuantity = 12;*/
        }
    }

    /// <summary>
    /// INCREASE QUALITY METH
    /// </summary>
    /// 
    public static class MethQualityAdd
    {
        public static bool Add = false;
    }

    [HarmonyPatch(typeof(LabOven), "Shatter")]
    public static class LabOven_QualityPatch
    {
        private static readonly HashSet<object> processedOperations = new HashSet<object>();

        [HarmonyPrefix]
        public static void Prefix(LabOven __instance)
        {
            if (__instance.CurrentOperation == null) return;

            var op = __instance.CurrentOperation;

            if (processedOperations.Contains(op)) return;

            if (op.IngredientQuality < EQuality.Heavenly)
            {
                MelonLogger.Msg($"__instance.CurrentOperation.IngredientQuality {__instance.CurrentOperation.IngredientQuality}");
                __instance.CurrentOperation.IngredientQuality += 1;
                processedOperations.Add(op);
                MelonLogger.Msg($"__instance.CurrentOperation.IngredientQuality {__instance.CurrentOperation.IngredientQuality}");
                MelonCoroutines.Start(CleanUp(op));
            }
        }

        private static System.Collections.IEnumerator CleanUp(object id)
        {
            yield return new WaitForSeconds(1f);
            processedOperations.Remove(id);
        }
    }

}
