using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using PavonisInteractive.TerraInvicta;
using CouncilorLevels;

namespace OrgCapFromCouncilorLevel
{
    static class Main
    {
        public static bool enabled;
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(TICouncilorState), "SufficientCapacityForOrg")]
    static class SufficientCapacityForOrgPatch
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="org"></param>
        static bool Prefix(TIOrgState org, TICouncilorState __instance, ref bool __result)
        {
            __result = __instance.orgs.Count < 15 && __instance.orgsWeight + org.tier <= CouncilorLevelManagerExternalMethods.GetCouncilorLevel(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(TICouncilorState), "CanRemoveOrg_Admin")]
    static class CanRemoveOrg_AdminPatch
    {
        static bool Prefix(TIOrgState org, ref TICouncilorState __instance, ref bool __result)
        {
            // Because org capacity is driven by level we no longer need this check. You can always remove orgs.
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(TIFactionState), "ValidateAllOrgs")]
    static class ValidateAllOrgsPatch
    {
        static bool Prefix(bool suppressReporting, TIFactionState __instance, ref List<TIOrgState> __result)
        {
            List<TIOrgState> badOrgs = new List<TIOrgState>();
            new List<int>();
            Func<TICouncilorState, TIOrgState> func = (TICouncilorState councilor) => (from x in councilor.orgs.Except(badOrgs)
                                                                                       // orderby x.administration
                                                                                       orderby x.tier
                                                                                       select x).First<TIOrgState>();
            foreach (TICouncilorState ticouncilorState in __instance.councilors)
            {
                int? availableAdministration = CouncilorLevelManagerExternalMethods.GetCouncilorLevel(ticouncilorState) - ticouncilorState.orgsWeight;
                Action<TIOrgState> action = delegate (TIOrgState badOrg)
                {
                    badOrgs.Add(badOrg);
                    // availableAdministration += badOrg.tier;
                };
                using (List<TIOrgState>.Enumerator enumerator2 = ticouncilorState.orgs.GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        TIOrgState tiorgState = enumerator2.Current;
                        if (!tiorgState.IsEligibleForCouncilor(ticouncilorState))
                        {
                            action(tiorgState);
                        }
                    }
                    goto IL_BE;
                }
                goto IL_AC;
            IL_BE:
                if (availableAdministration >= 0 || !ticouncilorState.orgs.Except(badOrgs).Any<TIOrgState>())
                {
                    continue;
                }
            IL_AC:
                TIOrgState tiorgState2 = func(ticouncilorState);
                action(tiorgState2);
                goto IL_BE;
            }
            foreach (TIOrgState tiorgState3 in badOrgs)
            {
                __instance.AddOrgToFactionPool(tiorgState3, tiorgState3.assignedCouncilor);
            }
            if (!suppressReporting && badOrgs.Count > 0)
            {
                TINotificationQueueState.LogOrgsForcedToPool(__instance, badOrgs);
            }
            __result = badOrgs;
            return false;
        }
    }

    // UI

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(CouncilGridController), "SetCouncilorInfo")]
    class SetCouncilorInfoPatch
    {
        static void Postfix(CouncilGridController __instance)
        {
            __instance.councilorOrgGridTitle.SetText(Loc.T("UI.Councilor.OrgGridTitle", new object[]
            {
                __instance.currentCouncilor.orgsWeight.ToString(),
                CouncilorLevelManagerExternalMethods.GetCouncilorLevel(__instance.currentCouncilor),
                __instance.currentCouncilor.orgs.Count.ToString(),
                15.ToString()
            }), true);
        }
    }

    // AI 

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(AIEvaluators), "EvaluateStatIncreaseUtility")]
    class AdjustAIAdminPreference
    {
        static void Postfix(TICouncilorState councilor, TIFactionState faction, CouncilorAttribute attribute, float __result, List<TIMissionTemplate> requiredMissions)
        {
            if (attribute == CouncilorAttribute.Administration)
            {
                // The admin value is weighted by 20!! We reduce that to 2f
                __result /= 10f;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(AIEvaluators), "EvaluateOrgForCouncilor")]
    class EvaluateOrgForCouncilorPatch
    {
        static bool Prefix(TIOrgState org, TICouncilorState councilor, List<TIMissionTemplate> requiredMissions, List<TIMissionTemplate> missingRequiredMissions, float __result)
        {
            if (org.template == councilor.faction.winningOrgTemplate)
            {
                if (councilor.traits.Any((TITraitTemplate x) => x.restrictedLocations > RestrictedLocations.None))
                {
                    __result = -1f;
                    return false;
                }
            }
            if (org.tier - org.administration > CouncilorLevelManagerExternalMethods.GetCouncilorLevel(councilor))
            {
                __result = 0f;
                return false;
            }
            if (org.incomeMoney_month < 0f && councilor.faction.GetDailyIncome(FactionResource.Money, false, false) < 0f)
            {
                __result = -1f;
                return false;
            }
            if (org.incomeInfluence_month < 0f && councilor.faction.GetDailyIncome(FactionResource.Influence, false, false) < 0f)
            {
                __result = -1f;
                return false;
            }
            if (org.incomeOps_month < 0f && councilor.faction.GetDailyIncome(FactionResource.Operations, false, false) < 0f)
            {
                __result = -1f;
                return false;
            }
            if (org.incomeBoost_month < 0f && councilor.faction.GetDailyIncome(FactionResource.Boost, false, false) < 0f)
            {
                __result = -1f;
                return false;
            }
            float num = 0f;
            TIFactionState faction = councilor.faction;
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Money, org.incomeMoney_month) * councilor.GetResourceMultiplierFromAttributes(FactionResource.Money);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Influence, org.incomeInfluence_month) * councilor.GetResourceMultiplierFromAttributes(FactionResource.Influence);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Boost, org.incomeBoost_month);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.MissionControl, org.incomeMissionControl);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Operations, org.incomeOps_month) * councilor.GetResourceMultiplierFromAttributes(FactionResource.Operations);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Research, org.incomeResearch_month) * councilor.GetResourceMultiplierFromAttributes(FactionResource.Research);
            num += AIEvaluators.EvaluateMonthlyResourceIncome(councilor.faction, FactionResource.Projects, (float)org.projectCapacityGranted);
            num += 5f * faction.aiValues.gatherInfluence * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Persuasion, (float)org.persuasion, requiredMissions);
            num += 5f * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Investigation, (float)org.investigation, requiredMissions);
            num += 5f * faction.aiValues.gatherOps * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Command, (float)org.command, requiredMissions);
            num += 5f * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Espionage, (float)org.espionage, requiredMissions);
            num += 5f * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Administration, (float)org.administration, requiredMissions) * faction.aiValues.gatherMoney;
            num += 5f * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Science, (float)org.science, requiredMissions) * faction.aiValues.gatherScience;
            num += 5f * AIEvaluators.EvaluateStatIncreaseUtility(councilor, faction, CouncilorAttribute.Security, (float)org.security, requiredMissions) * faction.aiValues.protectCouncilors;
            foreach (TIMissionTemplate timissionTemplate in org.missionsGranted)
            {
                if (!councilor.GetPossibleMissionList(false, false, true, null).Contains(timissionTemplate))
                {
                    num += AIEvaluators.EvaluateMissionTemplateUtility(councilor, timissionTemplate, requiredMissions, missingRequiredMissions);
                }
            }
            if (org.projectGranted != null && !faction.completedProjects.Contains(org.projectGranted))
            {
                num += org.projectGranted.GetResearchCost(faction) / 100f;
            }
            foreach (TechBonus techBonus in org.techBonuses)
            {
                num += techBonus.bonus * 100f;
            }
            num += org.economyBonus * 100f;
            num += org.welfareBonus * 100f;
            num += org.knowledgeBonus * 100f * faction.aiValues.gatherScience;
            num += org.unityBonus * 100f * faction.aiValues.wantPopularity;
            num += org.militaryBonus * 120f * faction.aiValues.wantEarthWarCapability;
            num += org.spoilsBonus * 100f * faction.aiValues.gatherMoney;
            num += org.spaceDevBonus * 100f * faction.aiValues.gatherMoney;
            num += org.spaceflightBonus * 300f * Mathf.Max(faction.aiValues.wantSpaceFacilities, faction.aiValues.wantSpaceWarCapability);
            if (councilor.faction.IsActiveHumanFaction)
            {
                num += org.miningBonus * 300f;
            }
            __result = num;
            return false;
        }
}
}