using System;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    [DefaultExecutionOrder(100)]
    public sealed class RuleBasedCommander : MonoBehaviour, ICommander
    {
        private static readonly ProfilerMarker ThinkMarker =
            new("TinyKingdom.AI.RuleBasedCommander.Think");

        [SerializeField] private Faction faction = Faction.Red;
        [SerializeField] private bool commanderEnabled = true;
        [SerializeField, Min(0.1f)] private float thinkInterval = 0.50f;
        [SerializeField] private FactionKnowledge knowledge;
        [SerializeField] private FactionCommandService commandService;
        [SerializeField] private EconomyCommanderPolicy economyPolicy = new();
        [SerializeField] private ScoutingCommanderPolicy scoutingPolicy = new();
        [SerializeField] private bool drawScoutGizmo = false;
        [Header("Runtime debug (disabled by default)")]
        [SerializeField] private bool exposeRuntimeDebug = false;
        [SerializeField] private ulong debugThinkTick;
        [SerializeField] private int debugKnownFood;
        [SerializeField] private int debugKnownWood;
        [SerializeField] private int debugKnownGold;
        [SerializeField] private int debugFoodAssignments;
        [SerializeField] private int debugWoodAssignments;
        [SerializeField] private int debugGoldAssignments;
        [SerializeField] private UnitController debugScout;
        [SerializeField] private Vector2Int debugScoutDestination;
        [SerializeField] private int debugExploredCells;
        [SerializeField] private int debugVisibleEnemyUnits;
        [SerializeField] private int debugKnownEnemyBuildings;

        private float nextThinkTime;

        public Faction Faction => faction;
        public bool CommanderEnabled => commanderEnabled;
        public float ThinkInterval => thinkInterval;
        public ulong ThinkTick { get; private set; }
        public FactionKnowledge Knowledge => knowledge;
        public FactionCommandService CommandService => commandService;
        public EconomyCommanderPolicy EconomyPolicy => economyPolicy;
        public ScoutingCommanderPolicy ScoutingPolicy => scoutingPolicy;
        public UnitController CurrentScout => scoutingPolicy?.Scout;
        public bool IsConfigured => FactionRelations.IsPlayable(faction) && knowledge != null
            && commandService != null && knowledge.Faction == faction
            && commandService.Faction == faction;

        public void Configure(Faction owner, FactionKnowledge factionKnowledge,
            FactionCommandService commands, float interval = 0.50f)
        {
            faction = owner;
            knowledge = factionKnowledge;
            commandService = commands;
            thinkInterval = Mathf.Max(0.1f, interval);
            nextThinkTime = Time.time;
        }

        private void OnEnable()
        {
            nextThinkTime = Time.time;
        }

        private void OnDisable()
        {
            economyPolicy?.Reset();
            scoutingPolicy?.Reset();
            ThinkTick = 0;
        }

        private void Update()
        {
            if (!commanderEnabled || !IsConfigured || Time.time < nextThinkTime) return;
            MatchManager match = MatchManager.Instance;
            if (match == null || match.State != MatchState.Playing) return;
            nextThinkTime = Time.time + thinkInterval;
            Think(new CommanderDecisionContext(faction, Time.time, knowledge, commandService));
        }

        public void Think(CommanderDecisionContext context)
        {
            if (!commanderEnabled || context.Faction != faction
                || MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing) return;
            using (ThinkMarker.Auto())
            {
                context.Knowledge.Refresh();
                scoutingPolicy.Tick(context);
                economyPolicy.Tick(context, scoutingPolicy.Scout);
                ThinkTick++;
                if (exposeRuntimeDebug) RefreshDebug();
            }
        }

        private void RefreshDebug()
        {
            debugThinkTick = ThinkTick;
            debugKnownFood = debugKnownWood = debugKnownGold = 0;
            for (int i = 0; i < knowledge.KnownResources.Count; i++)
            {
                switch (knowledge.KnownResources[i].ResourceType)
                {
                    case ResourceType.Food: debugKnownFood++; break;
                    case ResourceType.Wood: debugKnownWood++; break;
                    case ResourceType.Gold: debugKnownGold++; break;
                }
            }
            debugFoodAssignments = economyPolicy.FoodAssignments;
            debugWoodAssignments = economyPolicy.WoodAssignments;
            debugGoldAssignments = economyPolicy.GoldAssignments;
            debugScout = scoutingPolicy.Scout;
            debugScoutDestination = scoutingPolicy.HasDestination
                ? scoutingPolicy.DestinationCell : new Vector2Int(-1, -1);
            debugExploredCells = knowledge.ExploredCellCount;
            debugVisibleEnemyUnits = knowledge.VisibleEnemyUnits.Count;
            debugKnownEnemyBuildings = knowledge.KnownEnemyBuildings.Count;
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!drawScoutGizmo || scoutingPolicy == null || !scoutingPolicy.HasDestination
                || knowledge == null || knowledge.TeamVision?.GridManager == null) return;
            Vector2Int cell = scoutingPolicy.DestinationCell;
            Vector3 position = knowledge.TeamVision.GridManager.GridToWorld(cell.x, cell.y);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(position, 0.35f);
            if (scoutingPolicy.Scout != null)
                Gizmos.DrawLine(scoutingPolicy.Scout.transform.position, position);
#endif
        }
    }
}
