#if UNITY_EDITOR
using System;
using System.Reflection;
using TinyKingdom.Audio;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H3PawnScoutValidation
    {
        private const string PawnDataPath = "Assets/Data/PawnCombatData.asset";
        private const string WarriorDataPath = "Assets/Data/WarriorCombatData.asset";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.3 Pawn Scout")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run Phase 6H.3 validation in Edit Mode.");
                return;
            }

            GameObject root = null;
            try
            {
                RtsHudAssetCatalog hud = RtsHudAssetCatalog.Load();
                GameplayAudioCatalog audio = GameplayAudioCatalog.Load();
                UnitCombatData pawnData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(PawnDataPath);
                UnitCombatData warriorData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(WarriorDataPath);
                Require(PawnScoutPromotion.WoodCost == 50, "Scout cost must be exactly 50 Wood.");
                Require(Mathf.Approximately(PawnScoutPromotion.ScoutVisionMultiplier, 2.5f),
                    "Scout vision multiplier must be exactly 2.5.");
                Require(Mathf.Approximately(PawnScoutPromotion.FireVisualScale, 0.7f),
                    "Scout fire marker scale must be exactly 0.70.");
                Require(hud != null && hud.HasScoutPresentation,
                    "RtsHudAssets must reference the verified Fire.png icon and seven flame frames.");
                Require(audio != null && audio.TryGetCue(GameplayAudioEvent.BuildingFire,
                        out GameplayAudioCue fireCue) && fireCue.Clip != null,
                    "Scout ambience must reuse the configured BuildingFire cue.");
                Require(audio != null && audio.TryGetCue(GameplayAudioEvent.ScoutPromotion,
                        out GameplayAudioCue promotionCue) && promotionCue.Clip != null,
                    "Scout promotion must have a configured success sound.");
                Require(audio != null && audio.TryGetCue(
                        GameplayAudioEvent.UnitProductionFinished,
                        out GameplayAudioCue productionCue) && productionCue.Clip != null,
                    "Successful human unit spawn must retain its completion sound.");
                Require(PawnScoutPromotion.FireAudioVolumeMultiplier > 0f
                    && PawnScoutPromotion.FireAudioVolumeMultiplier <= 0.4f,
                    "Scout fire ambience must use a quiet 0.01-0.40 volume multiplier.");
                Require(!MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Pawn),
                    "Pawn/Scout must remain excluded from military formations.");

                root = new GameObject("Phase6H3_Validation");
                root.SetActive(false);
                ResourceStockpile stockpile = root.AddComponent<ResourceStockpile>();
                stockpile.Configure(Faction.Blue, 0, 100, 0);
                MatchManager match = root.AddComponent<MatchManager>();
                match.Configure(Faction.Blue);
                SetMatchState(match, MatchState.Playing);
                UnitController first = CreateUnit(root.transform, "PawnA", pawnData);
                UnitController second = CreateUnit(root.transform, "PawnB", pawnData);
                UnitController warrior = CreateUnit(root.transform, "Warrior", warriorData);
                root.SetActive(true);

                Require(PawnScoutPromotion.Evaluate(first, Faction.Blue, stockpile,
                        hud.ScoutFireFrames) == PawnScoutEligibility.Eligible,
                    "A living friendly Pawn with 50+ Wood must be eligible.");
                Require(PawnScoutPromotion.Evaluate(warrior, Faction.Blue, stockpile,
                        hud.ScoutFireFrames) == PawnScoutEligibility.InvalidPawn,
                    "A non-Pawn must be rejected.");
                Require(PawnScoutPromotion.TryPromote(first, Faction.Blue, stockpile,
                        hud.ScoutFireFrames),
                    "The first valid Pawn promotion failed.");
                Require(stockpile.GetAmount(ResourceType.Wood) == 50,
                    "Successful promotion must deduct exactly 50 Wood.");
                PawnScoutPromotion promoted = first.GetComponent<PawnScoutPromotion>();
                Require(promoted != null && promoted.IsPromoted
                    && promoted.FirePlayer != null && promoted.FirePlayer.FrameCount == 7,
                    "Promoted Pawn must own one configured seven-frame Scout fire visual.");
                Require(first.GetComponent<HitSquashStretchPresentation>() != null,
                    "Promoted Pawn must use squash/stretch feedback without requiring hit flash.");
                Require(Mathf.Approximately(promoted.transform.Find("PawnScoutFire")
                        .localScale.x, PawnScoutPromotion.FireVisualScale),
                    "Scout fire visual scale is not configured.");
                Require(Mathf.Approximately(first.VisionRadius,
                        first.BaseVisionRadius * PawnScoutPromotion.ScoutVisionMultiplier),
                    "Scout vision must derive from authoritative Pawn vision x2.5.");

                int woodBeforeRejected = stockpile.GetAmount(ResourceType.Wood);
                Require(PawnScoutPromotion.Evaluate(second, Faction.Blue, stockpile,
                        hud.ScoutFireFrames) == PawnScoutEligibility.ActiveScoutExists,
                    "A second Pawn must be blocked while the first Scout lives.");
                Require(!PawnScoutPromotion.TryPromote(second, Faction.Blue, stockpile,
                        hud.ScoutFireFrames)
                    && stockpile.GetAmount(ResourceType.Wood) == woodBeforeRejected,
                    "Rejected promotion must not spend Wood.");

                first.GetComponent<HealthComponent>().KillAt(first.transform.position);
                Require(PawnScoutPromotion.ActiveFor(Faction.Blue) == null,
                    "Scout death must release the one-active registry slot.");
                Require(PawnScoutPromotion.TryPromote(second, Faction.Blue, stockpile,
                        hud.ScoutFireFrames),
                    "A new Pawn must be promotable after Scout death.");

                Debug.Log("Phase 6H.3 Pawn Scout validation PASS: 50W cost, Pawn-only "
                    + "eligibility, one-active registry, x2.5 derived vision, verified Fire.png "
                    + "visual, quiet BuildingFire loop reuse and non-military formation status.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 6H.3 Pawn Scout validation FAILED: " + exception);
                throw;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static UnitController CreateUnit(Transform parent, string name,
            UnitCombatData data)
        {
            var obj = new GameObject(name);
            obj.SetActive(false);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<SpriteRenderer>();
            UnitController controller = obj.AddComponent<UnitController>();
            controller.Configure(Faction.Blue);
            HealthComponent health = obj.AddComponent<HealthComponent>();
            health.Configure(data);
            obj.SetActive(true);
            return controller;
        }

        private static void SetMatchState(MatchManager match, MatchState state)
        {
            FieldInfo field = typeof(MatchManager).GetField("<State>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("MatchState backing field changed.");
            field.SetValue(match, state);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
