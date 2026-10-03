#if UNITY_EDITOR
using System;
using System.IO;
using TinyKingdom.Audio;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H5ConstructionFeedbackValidation
    {
        private const string BuilderPath = "Assets/Scripts/Buildings/PawnBuilder.cs";
        private const string VisualPath = "Assets/Scripts/Economy/PawnVisual.cs";
        private const string BlueAnimationPath = "Assets/UnitAnimations/BluePawn.asset";
        private const string RedAnimationPath = "Assets/UnitAnimations/RedPawn.asset";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.5 Construction Feedback")]
        public static void Validate()
        {
            Require(Mathf.Approximately(PawnBuilder.WorkCycleSeconds, 0.8f),
                "Authoritative Pawn work cycle changed from 0.8 seconds.");
            Require(Mathf.Approximately(PawnBuilder.RepairImpactDelay, 0.35f),
                "Authoritative repair impact changed from 0.35 seconds.");
            Require(Mathf.Approximately(PawnVisual.BuildSwingDuration, 0.6f)
                && Mathf.Approximately(PawnVisual.BuildSwingRecoverySeconds, 0.2f)
                && Mathf.Approximately(PawnVisual.BuildSwingDuration
                    + PawnVisual.BuildSwingRecoverySeconds,
                    PawnBuilder.WorkCycleSeconds),
                "Hammer swing/recovery cadence must fill one 0.8-second work cycle.");

            ValidateAnimationSet(BlueAnimationPath, "Blue");
            ValidateAnimationSet(RedAnimationPath, "Red");
            ValidateAudio();
            ValidateEventOwnership();

            int activeBuilders = 0;
            if (Application.isPlaying)
            {
                foreach (UnitController unit in UnitController.AllUnits)
                {
                    if (unit == null || unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                        != UnitType.Pawn) continue;
                    PawnBuilder builder = unit.GetComponent<PawnBuilder>();
                    PawnVisual visual = unit.GetComponent<PawnVisual>();
                    Require(builder != null && visual != null,
                        $"{unit.name}: Pawn builder/visual component is missing.");
                    if (!builder.IsWorking) continue;
                    activeBuilders++;
                    Require(visual.IsBuildFeedbackActive,
                        $"{unit.name}: working Pawn has no Hammer swing/recovery state.");
                    Require(!visual.IsBuildAnimationActive || !visual.IsLooping,
                        $"{unit.name}: Hammer impact animation is looping continuously.");
                }
            }

            Debug.Log("Phase 6H.5 Construction Feedback validation PASS: 0.8-second "
                + "authoritative work cycle and 0.35-second repair impact unchanged; verified "
                + "three-frame Hammer mapping plays once for 0.6 seconds with 0.2-second "
                + "recovery; one bounded trigger subscription; two Hammer clips preserved "
                + "with 0.7-second playback limit."
                + (Application.isPlaying
                    ? $" Runtime active builders checked: {activeBuilders}."
                    : " Run again in Play Mode while Pawns are building and repairing."));
        }

        private static void ValidateAnimationSet(string path, string faction)
        {
            PawnAnimationSet set = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(path);
            Require(set != null, $"{faction} PawnAnimationSet is missing.");
            Require(set.Hammer.Length == 3 && set.IdleHammer.Length > 0
                && set.RunHammer.Length > 0,
                $"{faction} Pawn Hammer/Idle Hammer/Run Hammer mapping is incomplete.");
            for (int i = 0; i < set.Hammer.Length; i++)
                Require(set.Hammer[i] != null,
                    $"{faction} Pawn Hammer frame {i} is missing.");
        }

        private static void ValidateAudio()
        {
            GameplayAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameplayAudioCatalog>(
                GameplayAudioSetup.CatalogPath);
            Require(catalog != null, "GameplayAudioCatalog is missing.");
            Require(catalog.TryGetCue(GameplayAudioEvent.PawnHammer,
                out GameplayAudioCue cue), "PawnHammer audio cue is missing.");
            Require(cue.ClipCount == 2,
                "PawnHammer must retain its two random clip variations.");
            Require(Mathf.Approximately(cue.MaxPlaybackSeconds, 0.7f)
                && cue.MaxPlaybackSeconds < PawnBuilder.WorkCycleSeconds,
                "PawnHammer playback must end before the next work event.");
        }

        private static void ValidateEventOwnership()
        {
            string builder = File.ReadAllText(BuilderPath);
            string visual = File.ReadAllText(VisualPath);
            Require(builder.Contains("target.AdvanceConstruction(Time.deltaTime);"),
                "Construction progress is no longer driven by the authoritative gameplay tick.");
            Require(Count(builder,
                    "GameplayAudioService.Request(GameplayAudioEvent.PawnHammer") == 1,
                "PawnBuilder must own exactly one centralized Hammer audio request path.");
            Require(!visual.Contains("GameplayAudioEvent.PawnHammer"),
                "PawnVisual must not create Hammer audio events.");
            Require(Count(visual, "builder.WorkCycleStarted += OnBuildWorkCycleStarted;") == 1
                && Count(visual,
                    "builder.WorkCycleStarted -= OnBuildWorkCycleStarted;") == 1,
                "PawnVisual must subscribe and unsubscribe exactly once to each builder cycle.");
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(token, index,
                StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
