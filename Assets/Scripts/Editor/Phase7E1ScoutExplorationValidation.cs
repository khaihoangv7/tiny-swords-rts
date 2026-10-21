#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.UI;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E1ScoutExplorationValidation
    {
        private const string PawnDataPath = "Assets/Data/PawnCombatData.asset";
        private const string WarriorDataPath = "Assets/Data/WarriorCombatData.asset";
        private const string ArcherDataPath = "Assets/Data/ArcherCombatData.asset";
        private const string LancerDataPath = "Assets/Data/LancerCombatData.asset";
        private const string MonkDataPath = "Assets/Data/MonkCombatData.asset";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.1 Scout & Exploration")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ValidateDebugAidRuntime();
                return;
            }

            GameObject root = null;
            MatchManager previousMatch = MatchManager.Instance;
            FieldInfo matchInstanceField = typeof(MatchManager).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                RtsHudAssetCatalog hud = RtsHudAssetCatalog.Load();
                UnitCombatData pawnData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    PawnDataPath);
                UnitCombatData warriorData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    WarriorDataPath);
                UnitCombatData archerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    ArcherDataPath);
                UnitCombatData lancerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    LancerDataPath);
                UnitCombatData monkData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    MonkDataPath);
                Require(hud != null && hud.HasScoutPresentation,
                    "The shared Scout presentation catalog is incomplete.");
                Require(pawnData != null && warriorData != null && archerData != null
                    && lancerData != null && monkData != null,
                    "Required unit combat data is missing.");
                Require(PawnScoutPromotion.WoodCost == 50,
                    "Scout promotion cost must remain exactly 50 Wood.");
                Require(Mathf.Approximately(PawnScoutPromotion.ScoutVisionMultiplier, 2.5f),
                    "Scout vision must remain Pawn vision x2.5.");
                Require(!MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Pawn),
                    "Promoted Pawn Scouts must remain formation-ineligible.");
                Require(typeof(FactionCommandService).GetMethod("TryPromoteScout",
                        BindingFlags.Instance | BindingFlags.Public) != null,
                    "Scout promotion must be exposed through FactionCommandService.");
                ValidateDebugAidContract();

                var policy = new ScoutingCommanderPolicy();
                Require(policy.MaxFrontierCandidates > 0 && policy.MaxFrontierCandidates <= 96,
                    "Frontier scoring must use a bounded candidate set.");
                Require(policy.InformationGainWeight > 0f && policy.RiskWeight > 0f
                    && policy.TravelWeight > 0f && policy.StrategicRevisitWeight > 0f,
                    "Scout frontier/re-scout scoring weights are not configured.");
                Require(policy.UnknownRegionWeight > 0f
                    && policy.ExplorationDepthWeight > 0f
                    && policy.ReconStrategicWeight > 0f,
                    "Deep exploration and reconnaissance weights are not configured.");
                Require(policy.MaxReconCandidates > 0 && policy.MaxReconCandidates <= 64,
                    "Recon observation generation must remain bounded.");
                Require(policy.RecentTargetCapacity >= 4
                    && policy.RecentTargetCapacity <= 16,
                    "Recent Scout target suppression must use bounded storage.");
                Require(policy.ExplorationRiskBudget == StrategicRisk.Safe
                    && policy.ReconRiskBudget == StrategicRisk.Risky,
                    "Scout risk budgets must keep exploration low-risk and recon moderate-risk.");
                Require(typeof(ScoutingCommanderPolicy).GetMethod("Tick", new[]
                    {
                        typeof(CommanderDecisionContext), typeof(CommanderPawnRoles),
                        typeof(StrategicSafetyMap), typeof(BuildingCommanderPolicy)
                    }) != null,
                    "Scouting policy must consume StrategicSafetyMap and building reserve state.");
                Require(typeof(BuildingKnowledgeSnapshot).GetProperty("LastSeenTime") != null
                    && typeof(ResourceKnowledgeSnapshot).GetProperty("LastSeenTime") != null,
                    "FactionKnowledge snapshots must expose legal last-seen timestamps.");
                Require(typeof(FactionKnowledge).GetProperty("LastMilitaryObservation") != null,
                    "FactionKnowledge must expose the legal military memory snapshot.");
                Require(typeof(MilitaryObservationSnapshot).GetProperty("WarriorCount") != null
                    && typeof(MilitaryObservationSnapshot).GetProperty("ArcherCount") != null
                    && typeof(MilitaryObservationSnapshot).GetProperty("LancerCount") != null
                    && typeof(MilitaryObservationSnapshot).GetProperty("MonkCount") != null
                    && typeof(MilitaryObservationSnapshot).GetProperty("LastSeenTime") != null
                    && typeof(MilitaryObservationSnapshot).GetProperty(
                        "ObservationRegion") != null,
                    "Military memory is missing a required value-only observation field.");
                RequireNoLiveReferences(typeof(MilitaryObservationSnapshot));
                RequireNoHiddenTargetReferences();

                root = new GameObject("Phase7E1_Validation");
                root.SetActive(false);
                ResourceStockpile stockpile = root.AddComponent<ResourceStockpile>();
                stockpile.Configure(Faction.Red, 0, 49, 0);
                MatchManager match = root.AddComponent<MatchManager>();
                match.Configure(Faction.Blue);
                SetMatchState(match, MatchState.Playing);
                FactionKnowledge knowledge = root.AddComponent<FactionKnowledge>();
                knowledge.Configure(Faction.Red, null, null, stockpile, null);
                ValidateMilitarySnapshotFreeze(knowledge);
                FactionCommandService commands = root.AddComponent<FactionCommandService>();
                commands.Configure(Faction.Red, knowledge);
                UnitController first = CreatePawn(root.transform, "RedPawnA", pawnData);
                UnitController second = CreatePawn(root.transform, "RedPawnB", pawnData);
                UnitController warrior = CreateUnit(root.transform, "RedWarrior", warriorData);
                UnitController archer = CreateUnit(root.transform, "RedArcher", archerData);
                UnitController lancer = CreateUnit(root.transform, "RedLancer", lancerData);
                UnitController monk = CreateUnit(root.transform, "RedMonk", monkData);
                root.SetActive(true);
                // Awake does not run on ordinary scene objects in Edit Mode.
                // Give command validation the fixture MatchManager explicitly.
                Require(matchInstanceField != null,
                    "MatchManager.Instance backing field changed.");
                matchInstanceField.SetValue(null, match);

                Require(commands.TryPromoteScout(first)
                        == CommanderCommandResult.RejectedInsufficientResources,
                    "Red must not receive a free Scout below 50 Wood.");
                Require(first.GetComponent<PawnScoutPromotion>() == null,
                    "A rejected promotion must not add Scout state.");
                Require(commands.TryPromoteScout(warrior)
                        == CommanderCommandResult.RejectedWrongUnitType,
                    "Warrior must never be accepted as the dedicated Scout.");
                Require(commands.TryPromoteScout(archer)
                        == CommanderCommandResult.RejectedWrongUnitType
                    && commands.TryPromoteScout(lancer)
                        == CommanderCommandResult.RejectedWrongUnitType
                    && commands.TryPromoteScout(monk)
                        == CommanderCommandResult.RejectedWrongUnitType,
                    "Archer, Lancer and Monk must never be accepted as dedicated Scouts.");

                stockpile.Add(ResourceType.Wood, 51);
                Require(commands.TryPromoteScout(first) == CommanderCommandResult.Accepted,
                    "FactionCommandService failed to promote a legal Red Pawn Scout.");
                Require(stockpile.GetAmount(ResourceType.Wood) == 50,
                    "Successful Red Scout promotion must deduct exactly 50 Wood.");
                PawnScoutPromotion promotion = first.GetComponent<PawnScoutPromotion>();
                Require(promotion != null && promotion.IsPromoted
                    && Mathf.Approximately(first.VisionRadius,
                        first.BaseVisionRadius * 2.5f),
                    "Red Scout must use the shared promotion component and x2.5 vision.");

                var roles = new CommanderPawnRoles();
                Require(roles.TryAssign(first, CommanderPawnRole.Scout)
                    && roles.GetRole(first) == CommanderPawnRole.Scout,
                    "Promoted Scout was not reserved by CommanderPawnRoles.");
                Require(!roles.TryAssign(first, CommanderPawnRole.Economy)
                    && !roles.TryAssign(first, CommanderPawnRole.Builder),
                    "Economy/Builder roles must not overwrite an active Scout.");
                Require(!ArmyGroup.IsEligible(first, Faction.Red, first),
                    "Active Scout must be excluded from ArmyGroup.");

                int beforeSecond = stockpile.GetAmount(ResourceType.Wood);
                Require(commands.TryPromoteScout(second)
                        == CommanderCommandResult.RejectedCommandFailed
                    && stockpile.GetAmount(ResourceType.Wood) == beforeSecond,
                    "A living Scout must block a second promotion without spending Wood.");

                first.GetComponent<HealthComponent>().KillAt(first.transform.position);
                Require(PawnScoutPromotion.ActiveFor(Faction.Red) == null,
                    "Scout death must clear the authoritative active registry.");
                Require(commands.TryPromoteScout(second) == CommanderCommandResult.Accepted,
                    "A replacement Red Pawn Scout should be legal after death when affordable.");

                Debug.Log("Phase 7E.1 Scout & Exploration validation PASS: authoritative "
                    + "50W Pawn promotion, one active Scout, x2.5 vision, Commander role "
                    + "exclusions, deep frontier progression, bounded recon observation rings, "
                    + "risk budgets, recent-target suppression and value-only military memory "
                    + "are configured.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.1 Scout & Exploration validation FAILED: "
                    + exception);
                throw;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                matchInstanceField?.SetValue(null, previousMatch);
            }
        }

        private static void ValidateDebugAidContract()
        {
            var speeds = MatchSpeedController.SupportedSpeeds;
            Require(speeds.Count == 4
                && Mathf.Approximately(speeds[0], 1f)
                && Mathf.Approximately(speeds[1], 2f)
                && Mathf.Approximately(speeds[2], 5f)
                && Mathf.Approximately(speeds[3], 10f),
                "Match speed options must be exactly x1, x2, x5 and x10.");
            Require(Mathf.Approximately(MatchSpeedController.DefaultSpeed, 1f),
                "A new match must default to x1 speed.");
            Require(typeof(MatchSpeedController).GetMethod("SetSpeed",
                    new[] { typeof(float) }) != null,
                "Match speed buttons must use the centralized controller.");
            Require(DebugMatchControlsPresenter.RequiredButtonRowWidth
                    <= DebugMatchControlsPresenter.PanelWidth
                        - 2f * DebugMatchControlsPresenter.HorizontalPadding,
                "Top-right controls do not fit inside their content width.");
            Require(!DebugObserverView.IsEnabled,
                "Observer presentation must default to OFF.");

            FieldInfo[] controllerFields = typeof(MatchSpeedController).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < controllerFields.Length; i++)
            {
                Type type = controllerFields[i].FieldType;
                Require(type != typeof(FactionKnowledge)
                    && type != typeof(TeamVisionSystem)
                    && type != typeof(Faction),
                    "MatchSpeedController must not hold faction, knowledge or vision state.");
            }
            FieldInfo[] observerFields = typeof(DebugObserverView).GetFields(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < observerFields.Length; i++)
            {
                Type type = observerFields[i].FieldType;
                Require(type != typeof(FactionKnowledge)
                    && type != typeof(TeamVisionSystem)
                    && type != typeof(FogMemorySystem),
                    "DebugObserverView must contain presentation state only.");
            }
        }

        private static void ValidateDebugAidRuntime()
        {
            MatchSpeedController[] controllers = UnityEngine.Object
                .FindObjectsByType<MatchSpeedController>();
            Require(controllers.Length == 1,
                "Exactly one active MatchSpeedController is required.");
            DebugMatchControlsPresenter presenter = UnityEngine.Object
                .FindAnyObjectByType<DebugMatchControlsPresenter>();
            Require(presenter != null && presenter.Root != null
                && presenter.SpeedController == controllers[0],
                "Top-right debug controls are missing or use another speed authority.");
            Require(presenter.SpeedButtonCount == 4
                && presenter.Root.anchorMin == Vector2.one
                && presenter.Root.anchorMax == Vector2.one
                && presenter.Root.pivot == Vector2.one,
                "Debug controls must contain four speed buttons anchored top-right.");
            Require(presenter.BlocksWorldInput,
                "Debug controls must block pointer raycasts from reaching the world.");
            Require(presenter.BlueFogRenderer != null
                && presenter.BlueFogRenderer.DisplayedFaction == Faction.Blue,
                "Fog debug toggle must target only the Blue FogRenderer.");

            MatchSpeedController controller = controllers[0];
            float baseline = controller.BaselineFixedDeltaTime;
            float[] speeds = { 1f, 2f, 5f, 10f };
            for (int i = 0; i < speeds.Length; i++)
            {
                Require(controller.SetSpeed(speeds[i])
                    && Mathf.Approximately(Time.timeScale, speeds[i])
                    && Mathf.Approximately(Time.fixedDeltaTime, baseline * speeds[i]),
                    $"x{speeds[i]:0} did not apply centralized scaled game time.");
            }
            Require(controller.SetSpeed(1f), "Validator could not restore x1 speed.");

            presenter.SetHideBlueFog(true);
            Require(presenter.IsBlueFogHidden && DebugObserverView.IsEnabled
                && !presenter.BlueFogRenderer.IsFogVisible,
                "Hide Fog did not enable observer presentation and disable the mask.");
            ValidateHiddenEntityObserverOverride();
            presenter.SetHideBlueFog(false);
            Require(!presenter.IsBlueFogHidden && !DebugObserverView.IsEnabled
                && presenter.BlueFogRenderer.IsFogVisible,
                "Fog ON did not restore authoritative presentation and the Fog mask.");

            Debug.Log("Phase 7E.1 debug aid runtime validation PASS: one centralized "
                + "x1/x2/x5/x10 controller, top-right raycast-blocking controls and "
                + "presentation-only observer Fog toggle restore correctly.");
        }

        private static void ValidateHiddenEntityObserverOverride()
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            if (vision == null || !vision.IsReady) return;
            IReadOnlyList<UnitController> units = UnitController.AllUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || unit.Faction == PlayerViewRelation.PlayerViewFaction
                    || vision.IsVisible(PlayerViewRelation.PlayerViewFaction,
                        unit.transform.position)) continue;
                FogPresentationGate gate = unit.GetComponent<FogPresentationGate>();
                Require(gate != null && gate.IsPresented,
                    "Observer mode did not reveal a hidden enemy presentation gate.");
                Require(!vision.IsVisible(PlayerViewRelation.PlayerViewFaction,
                        unit.transform.position),
                    "Observer mode changed authoritative TeamVision visibility.");
                return;
            }
        }

        private static UnitController CreatePawn(Transform parent, string name,
            UnitCombatData data)
        {
            UnitController controller = CreateUnit(parent, name, data);
            controller.gameObject.AddComponent<PawnGatherer>();
            controller.gameObject.AddComponent<PawnBuilder>();
            return controller;
        }

        private static UnitController CreateUnit(Transform parent, string name,
            UnitCombatData data)
        {
            var obj = new GameObject(name);
            obj.SetActive(false);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<SpriteRenderer>();
            UnitController controller = obj.AddComponent<UnitController>();
            obj.AddComponent<UnitMovement>();
            controller.Configure(Faction.Red);
            HealthComponent health = obj.AddComponent<HealthComponent>();
            health.Configure(data);
            obj.SetActive(true);
            return controller;
        }

        private static void RequireNoHiddenTargetReferences()
        {
            FieldInfo[] fields = typeof(ScoutingCommanderPolicy).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.Name.Contains("target", StringComparison.OrdinalIgnoreCase)) continue;
                Type type = field.FieldType;
                Require(type != typeof(Transform) && type != typeof(GameObject)
                    && type != typeof(BuildingSite) && type != typeof(ResourceNode),
                    "Scout target state retains a hidden live reference: " + field.Name);
            }
        }

        private static void RequireNoLiveReferences(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance
                | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldType = fields[i].FieldType;
                Require(!typeof(UnityEngine.Object).IsAssignableFrom(fieldType)
                    && fieldType != typeof(Transform) && fieldType != typeof(GameObject),
                    type.Name + " contains a live Unity reference: " + fields[i].Name);
            }
        }

        private static void ValidateMilitarySnapshotFreeze(FactionKnowledge knowledge)
        {
            FieldInfo field = typeof(FactionKnowledge).GetField("visibleEnemyUnits",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo refresh = typeof(FactionKnowledge).GetMethod(
                "RefreshMilitaryObservation", BindingFlags.Instance
                | BindingFlags.NonPublic);
            object visibleValue = field?.GetValue(knowledge);
            Require(visibleValue is List<VisibleUnitObservation> && refresh != null,
                "Validator could not access the military observation aggregation seam.");
            var visible = (List<VisibleUnitObservation>)visibleValue;
            visible.Add(new VisibleUnitObservation(default, Faction.Blue,
                UnitType.Warrior, new Vector3(2f, 4f), 10, 10, UnitState.Idle,
                ObservedThreatActivity.None, 1f, 1f));
            visible.Add(new VisibleUnitObservation(default, Faction.Blue,
                UnitType.Archer, new Vector3(4f, 4f), 10, 10, UnitState.Idle,
                ObservedThreatActivity.None, 4f, 1f));
            visible.Add(new VisibleUnitObservation(default, Faction.Blue,
                UnitType.Lancer, new Vector3(6f, 4f), 10, 10, UnitState.Idle,
                ObservedThreatActivity.None, 1f, 1f));
            visible.Add(new VisibleUnitObservation(default, Faction.Blue,
                UnitType.Monk, new Vector3(8f, 4f), 10, 10, UnitState.Idle,
                ObservedThreatActivity.None, 0f, 1f));
            refresh.Invoke(knowledge, new object[] { 12f });
            MilitaryObservationSnapshot first = knowledge.LastMilitaryObservation;
            Require(first.WarriorCount == 1 && first.ArcherCount == 1
                && first.LancerCount == 1 && first.MonkCount == 1
                && Mathf.Approximately(first.LastSeenTime, 12f)
                && first.ObservationRegion == new Vector3(5f, 4f),
                "Visible military observations were not aggregated correctly.");

            visible.Clear();
            refresh.Invoke(knowledge, new object[] { 30f });
            MilitaryObservationSnapshot hidden = knowledge.LastMilitaryObservation;
            Require(hidden.WarriorCount == first.WarriorCount
                && hidden.ArcherCount == first.ArcherCount
                && hidden.LancerCount == first.LancerCount
                && hidden.MonkCount == first.MonkCount
                && Mathf.Approximately(hidden.LastSeenTime, first.LastSeenTime)
                && hidden.ObservationRegion == first.ObservationRegion,
                "Military snapshot changed while no enemy military was visible.");
        }

        private static void SetMatchState(MatchManager match, MatchState state)
        {
            FieldInfo field = typeof(MatchManager).GetField("<State>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException(
                "MatchState backing field changed.");
            field.SetValue(match, state);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
