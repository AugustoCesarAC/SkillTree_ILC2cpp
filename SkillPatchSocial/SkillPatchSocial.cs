using HarmonyLib;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.ATM;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Il2CppScheduleOne.PlayerScripts.PlayerInventory;

namespace SkillTree.SkillPatchSocial
{

    public static class CustomerCache
    {
        public static Dictionary<string, float> OriginalMinSpend = new Dictionary<string, float>();
        public static Dictionary<string, float> OriginalMaxSpend = new Dictionary<string, float>();
        public static bool IsLoaded = false;

        public static void FillCache(List<Customer> customers)
        {
            if (IsLoaded) return; 

            foreach (var c in customers)
            {
                string key = c.CustomerData.name;
                if (!OriginalMinSpend.ContainsKey(key))
                {
                    OriginalMinSpend.Add(key, c.CustomerData.MinWeeklySpend);
                    OriginalMaxSpend.Add(key, c.CustomerData.MaxWeeklySpend);
                }
            }
            IsLoaded = true;
            MelonLogger.Msg("Customer spending history successfully stored!");
        }
    }

    public static class BusinessCache
    {
        public static Dictionary<string, float> LaunderCapacity = new Dictionary<string, float>();
        public static bool IsLoaded = false;

        public static void FillCache(List<Business> business)
        {
            if (IsLoaded) return; 

            foreach (var c in business)
            {
                string key = c.PropertyName;
                if (!LaunderCapacity.ContainsKey(key))
                    LaunderCapacity.Add(key, c.LaunderCapacity);
            }
            IsLoaded = true;
            MelonLogger.Msg("Business Laundering Memory successfully stored!");
        }
    }

    public static class ATMConfig
    {
        public static float MaxWeeklyLimit = 10000f;
    }

    [HarmonyPatch(typeof(ATMInterface))]
    public static class ATM_DynamicLimit_IL2CPP_Final_Patch
    {
        [HarmonyPatch("get_remainingAllowedDeposit")]
        [HarmonyPrefix]
        public static bool PrefixGetRemaining(ref float __result)
        {
            __result = Mathf.Max(0f, ATMConfig.MaxWeeklyLimit - ATM.WeeklyDepositSum);
            return false;
        }

        [HarmonyPatch("GetAmountFromIndex")]
        [HarmonyPrefix]
        public static bool PrefixGetAmount(int index, bool depositing, ref float __result)
        {
            if (index == -1 || index >= ATMInterface.amounts.Length)
            {
                __result = 0f;
                return false;
            }

            if (depositing && index == ATMInterface.amounts.Length - 1)
            {
                float remaining = Mathf.Max(0f, ATMConfig.MaxWeeklyLimit - ATM.WeeklyDepositSum);
                __result = Mathf.Min(NetworkSingleton<MoneyManager>.Instance.cashBalance, remaining);
                return false;
            }

            __result = (float)ATMInterface.amounts[index];
            return false;
        }

        [HarmonyPatch("SetSelectedAmount")]
        [HarmonyPrefix]
        public static bool PrefixSetSelected(ATMInterface __instance, float amount)
        {
            float remaining = Mathf.Max(0f, ATMConfig.MaxWeeklyLimit - ATM.WeeklyDepositSum);

            float onlineBalance = NetworkSingleton<MoneyManager>.Instance.sync___get_value_onlineBalance();

            float limitForOperation = (!__instance.depositing)
                ? onlineBalance
                : Mathf.Min(NetworkSingleton<MoneyManager>.Instance.cashBalance, remaining);

            // Acessamos os campos diretamente pela instância para evitar o erro do AccessTools
            __instance.selectedAmount = Mathf.Clamp(amount, 0f, limitForOperation);

            if (__instance.amountLabelText != null)
            {
                __instance.amountLabelText.text = MoneyManager.FormatAmount(__instance.selectedAmount);
            }

            return false;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void PostfixUpdate(ATMInterface __instance)
        {
            if (!__instance.isOpen) return;

            bool limitReached = ATM.WeeklyDepositSum >= ATMConfig.MaxWeeklyLimit;

            if (__instance.menu_DepositButton != null)
                __instance.menu_DepositButton.interactable = !limitReached;

            if (__instance.depositLimitText != null)
            {
                __instance.depositLimitText.text = MoneyManager.FormatAmount(ATM.WeeklyDepositSum) + " / " + MoneyManager.FormatAmount(ATMConfig.MaxWeeklyLimit);
                __instance.depositLimitText.color = limitReached ? new Color32(255, 75, 75, 255) : Color.white;
            }
        }

        [HarmonyPatch("UpdateAvailableAmounts")]
        [HarmonyPrefix]
        public static bool PrefixUpdateAmounts(ATMInterface __instance)
        {
            if (__instance.depositing)
            {
                float cash = NetworkSingleton<MoneyManager>.Instance.cashBalance;
                float remaining = Mathf.Max(0f, ATMConfig.MaxWeeklyLimit - ATM.WeeklyDepositSum);
                var buttons = __instance.amountButtons;

                for (int i = 0; i < ATMInterface.amounts.Length; i++)
                {
                    if (i >= buttons.Count) break;

                    float amountVal = (float)ATMInterface.amounts[i];
                    if (i == ATMInterface.amounts.Length - 1)
                        buttons[i].interactable = cash > 0f && remaining > 0f;
                    else
                        buttons[i].interactable = (cash >= amountVal) && (amountVal <= remaining);
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// UP CUSTOMER SAMPLE
    /// </summary>
    public static class CustomerSample
    {
        public static float AddSampleChance = 0f;
    }
    [HarmonyPatch(typeof(Customer), "GetSampleSuccess")]
    public class PatchSampleSuccessUI
    {
        private static int _depth = 0;
        [HarmonyPrefix]
        public static void Prefix()
        {
            _depth++;
        }

        [HarmonyPostfix]
        public static void Postfix(ref float __result, float __state)
        {
            if (_depth == 1)
            {
                if (CustomerSample.AddSampleChance <= 0) return;

                float origin = __result;

                __result = Mathf.Clamp(__result + CustomerSample.AddSampleChance, 0f, 1f);
                MelonLogger.Msg($"[Skill] Chance de Sample alterada de: {origin:P0} para {__result:P0}");
            }
            _depth--;
        }
    }


    /// <summary>
    /// UP ASSIGN CUSTOMER DEALER
    /// </summary>
    public static class DealerUpCustomer
    {
        public static int MaxCustomer = 8;
    }

    [HarmonyPatch(typeof(DealerManagementApp))]
    public class DealerManagementPatch
    {
        private static int GetDynamicLimit() => DealerUpCustomer.MaxCustomer;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(DealerManagementApp __instance)
        {
            CheckAndExpandUI(__instance);

            if (__instance.AssignCustomerButton != null)
            {
                __instance.AssignCustomerButton.transform.SetSiblingIndex(1);
            }

            if (__instance.CustomerTitleLabel != null)
            {
                __instance.CustomerTitleLabel.transform.SetAsFirstSibling();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Content);
        }

        [HarmonyPatch("SetDisplayedDealer")]
        [HarmonyPostfix]
        public static void SetDisplayedDealer_Postfix(DealerManagementApp __instance, Dealer dealer)
        {
            CheckAndExpandUI(__instance);

            int currentLimit = GetDynamicLimit();

            if (__instance.CustomerTitleLabel != null)
            {
                __instance.CustomerTitleLabel.text = $"Assigned Customers ({dealer.AssignedCustomers.Count}/{currentLimit})";
            }

            if (__instance.AssignCustomerButton != null)
            {
                __instance.AssignCustomerButton.gameObject.SetActive(dealer.AssignedCustomers.Count < currentLimit);
                __instance.AssignCustomerButton.transform.SetSiblingIndex(1);
            }

            for (int j = 0; j < __instance.CustomerEntries.Length; j++)
            {
                if (dealer.AssignedCustomers.Count > j)
                {
                    Customer customer = dealer.AssignedCustomers[j];
                    RectTransform entry = __instance.CustomerEntries[j];

                    entry.Find("Mugshot").GetComponent<Image>().sprite = customer.NPC.MugshotSprite;
                    entry.Find("Name").GetComponent<Text>().text = customer.NPC.fullName;

                    Button removeBtn = entry.Find("Remove").GetComponent<Button>();
                    removeBtn.onClick.RemoveAllListeners();
                    removeBtn.onClick.AddListener((UnityAction)(() => {
                        dealer.SendRemoveCustomer(customer.NPC.ID);
                        __instance.SetDisplayedDealer(dealer);
                    }));

                    entry.gameObject.SetActive(true);
                }
                else
                {
                    __instance.CustomerEntries[j].gameObject.SetActive(false);
                }
            }
        }

        private static void CheckAndExpandUI(DealerManagementApp __instance)
        {
            int targetLimit = GetDynamicLimit();

            if (__instance.CustomerEntries.Length < targetLimit)
            {
                List<RectTransform> entriesList = __instance.CustomerEntries.ToList();
                RectTransform template = entriesList[0];
                Transform listParent = template.parent;

                while (entriesList.Count < targetLimit)
                {
                    RectTransform newSlot = GameObject.Instantiate(template, listParent);
                    newSlot.name = "CustomerEntry_Mod_Slot_" + entriesList.Count;
                    entriesList.Add(newSlot);
                }

                __instance.CustomerEntries = entriesList.ToArray();
                LayoutRebuilder.ForceRebuildLayoutImmediate(__instance.Content);
            }
        }
    }

    /// <summary>
    /// BETTER SUPPLIER
    /// </summary>
    public static class SupplierUp
    {
        public static float SupplierInc = 1;
        public static int SupplierLimit = 10;
    }

    [HarmonyPatch(typeof(PhoneShopInterface))]
    public class PhoneShopGlobalPatch
    {
        [HarmonyPatch("CartChanged")]
        [HarmonyPrefix]
        public static bool CartChanged_Prefix(PhoneShopInterface __instance)
        {
            if (__instance == null) return true;

            int itemCount;
            float orderTotal = __instance.GetOrderTotal(out itemCount);

            __instance.UpdateOrderTotal();

            //MelonLogger.Msg($"Order Total: {orderTotal} | Items: {itemCount}");

            if (orderTotal > 0f && orderTotal <= __instance.orderLimit)
            {
                __instance.ConfirmButton.interactable = itemCount <= (SupplierUp.SupplierLimit);
                //MelonLogger.Msg($"Can Confirm: {__instance.ConfirmButton.interactable} (Limit: SupplierUp.SupplierLimit})");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Supplier), "GetDeadDropLimit")]
    public static class Supplier_GetDeadDropLimit_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Supplier __instance, ref float __result)
        {
            if (SupplierUp.SupplierLimit == 10) return true; 

            __result = __instance.MaxOrderLimit * SupplierUp.SupplierInc;

            return false; 
        }
    }

    /// <summary>
    /// BETTER BUSINESS
    /// </summary>
    public static class BetterBusiness
    {
        public static float Add = 0f;
    }

    [HarmonyPatch]
    public static class BusinessLaunderingPatch
    {
        // Handles the progression of minutes and partial payments every 4 hours (240 mins)
        [HarmonyPatch(typeof(Business), "MinsPass")]
        [HarmonyPrefix]
        public static bool Prefix_MinsPass(Business __instance, int mins)
        {

            string pName = __instance.propertyName;

            for (int i = 0; i < __instance.LaunderingOperations.Count; i++)
            {
                var op = __instance.LaunderingOperations[i];
                int oldMins = op.minutesSinceStarted;
                op.minutesSinceStarted += mins;

                if (op.minutesSinceStarted < op.completionTime_Minutes)
                {
                    int oldInterval = oldMins / 240;
                    int newInterval = op.minutesSinceStarted / 240;

                    if (newInterval > oldInterval)
                    {
                        float installment = Mathf.Ceil(op.amount / 6f);

                        if (Il2CppFishNet.InstanceFinder.IsServer)
                        {
                            NetworkSingleton<MoneyManager>.Instance.CreateOnlineTransaction(
                                $"Partial Laundering ({pName})",
                                installment, 1f, string.Empty);

                            MelonLogger.Msg($"[LaunderingMod] Partial payout of {installment} processed for {pName}");
                        }

                        Singleton<NotificationsManager>.Instance.SendNotification(
                            pName,
                            $"<color=#16F01C>{MoneyManager.FormatAmount(installment)}</color> Laundered (Partial)",
                            NetworkSingleton<MoneyManager>.Instance.LaunderingNotificationIcon);
                    }
                }

                if (op.minutesSinceStarted >= op.completionTime_Minutes)
                {
                    op.amount = op.amount / 6f;

                    __instance.CompleteOperation(op);

                    MelonLogger.Msg($"[LaunderingMod] Operation completed for {pName}. Final installment paid.");
                    i--;
                }
            }

            return false;
        }
    }

}
