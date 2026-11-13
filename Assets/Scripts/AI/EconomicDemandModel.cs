using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Central value model for short-horizon economic decisions. Paid queue and
    // construction costs are already absent from the authoritative stockpile, so
    // only unpaid plans are reserved here.
    [Serializable]
    public sealed class EconomicDemandModel
    {
        private static readonly ProfilerMarker RefreshMarker =
            new("TinyKingdom.AI.EconomicDemand");
        [SerializeField, Min(0)] private int foodBuffer = 80;
        [SerializeField, Min(0)] private int woodBuffer = 100;
        [SerializeField, Min(0)] private int goldBuffer = 50;
        [SerializeField, Min(0)] private int criticalDeficit = 100;
        private ResourceType? externalPriority;
        private float externalPriorityUntil;

        public void SetExternalPriority(ResourceType type, float now)
        {
            externalPriority = type;
            externalPriorityUntil = now + 12f;
        }

        public void ClearExternalPriority() => externalPriority = null;

        public int Food { get; private set; }
        public int Wood { get; private set; }
        public int Gold { get; private set; }
        public int CarriedGold { get; private set; }
        public int GoldInTransit { get; private set; }
        public int ExpectedGold { get; private set; }
        public int GoldPlanningDeficit { get; private set; }
        public int ReservedFood { get; private set; }
        public int ReservedWood { get; private set; }
        public int ReservedGold { get; private set; }
        public int FoodDeficit { get; private set; }
        public int WoodDeficit { get; private set; }
        public int GoldDeficit { get; private set; }
        public int PopulationForecast { get; private set; }
        public int PopulationHeadroom { get; private set; }
        public string Reason { get; private set; } = "Initial balance";

        public void Reset()
        {
            Food = Wood = Gold = 0;
            CarriedGold = GoldInTransit = ExpectedGold = GoldPlanningDeficit = 0;
            ReservedFood = ReservedWood = ReservedGold = 0;
            FoodDeficit = WoodDeficit = GoldDeficit = 0;
            PopulationForecast = PopulationHeadroom = 0;
            Reason = "Reset";
            externalPriority = null;
            externalPriorityUntil = 0f;
        }

        public void Refresh(CommanderDecisionContext context,
            BuildingCommanderPolicy buildings, ProductionCommanderPolicy production,
            ScoutingCommanderPolicy scouting)
        {
            using var marker = RefreshMarker.Auto();
            FactionKnowledge knowledge = context.Knowledge;
            ResourceStockpile stockpile = knowledge?.Stockpile;
            Food = stockpile != null ? stockpile.GetAmount(ResourceType.Food) : 0;
            Wood = stockpile != null ? stockpile.GetAmount(ResourceType.Wood) : 0;
            Gold = stockpile != null ? stockpile.GetAmount(ResourceType.Gold) : 0;
            RefreshGoldPipeline(knowledge, context.Faction);
            ReservedFood = ReservedWood = ReservedGold = 0;

            BuildingSystem system = context.Commands?.BuildingSystem;
            if (buildings != null && buildings.ActiveSite == null
                && buildings.PlannedBuilding.HasValue && system != null)
                Add(system.GetData(buildings.PlannedBuilding.Value));

            if (production != null && production.TryGetPendingEconomicCost(
                out int food, out int wood, out int gold, out int population))
            {
                ReservedFood += food;
                ReservedWood += wood;
                ReservedGold += gold;
            }
            else population = 0;

            if (scouting != null && scouting.Scout == null
                && scouting.HasPendingPromotionDemand)
                ReservedWood += PawnScoutPromotion.WoodCost;

            FoodDeficit = Mathf.Max(0, foodBuffer + ReservedFood - Food);
            WoodDeficit = Mathf.Max(0, woodBuffer + ReservedWood - Wood);
            GoldDeficit = Mathf.Max(0, goldBuffer + ReservedGold - Gold);
            // The pipeline influences worker demand only. Building, training and
            // upgrade affordability continue to use deposited stockpile Gold.
            GoldPlanningDeficit = Mathf.Max(0, GoldDeficit - GoldInTransit
                - ExpectedGold / 2);

            FactionPopulation cap = context.Knowledge?.Population;
            PopulationForecast = cap != null ? cap.Used + cap.Reserved + population : population;
            PopulationHeadroom = cap != null ? cap.Cap - PopulationForecast : 0;
            ResourceType highest = HighestDemandType;
            Reason = $"{highest} planning deficit {PlanningDeficit(highest)}; unpaid plan "
                + $"F{ReservedFood}/W{ReservedWood}/G{ReservedGold}";
        }

        public ResourceType HighestDemandType
        {
            get
            {
                if (FoodDeficit >= WoodDeficit && FoodDeficit >= GoldPlanningDeficit)
                    return ResourceType.Food;
                return WoodDeficit >= GoldPlanningDeficit
                    ? ResourceType.Wood : ResourceType.Gold;
            }
        }

        public int Deficit(ResourceType type) => type switch
        {
            ResourceType.Food => FoodDeficit,
            ResourceType.Wood => WoodDeficit,
            ResourceType.Gold => GoldDeficit,
            _ => 0
        };

        public int PlanningDeficit(ResourceType type)
            => type == ResourceType.Gold ? GoldPlanningDeficit : Deficit(type);

        public bool IsCritical(ResourceType type)
            => PlanningDeficit(type) >= criticalDeficit;

        public float DemandWeight(ResourceType type)
        {
            float baseline = type switch
            {
                ResourceType.Food => 1.15f,
                ResourceType.Wood => 1.05f,
                ResourceType.Gold => 0.8f,
                _ => 0f
            };
            return baseline + PlanningDeficit(type) / 60f
                + (externalPriority == type && Time.time <= externalPriorityUntil
                    ? 1.5f : 0f);
        }

        private void RefreshGoldPipeline(FactionKnowledge knowledge, Faction faction)
        {
            CarriedGold = GoldInTransit = ExpectedGold = 0;
            if (knowledge == null) return;
            int mineLoad = 0;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                PawnGatherer gatherer = units[i]?.GetComponent<PawnGatherer>();
                if (gatherer == null) continue;
                if (mineLoad == 0)
                    mineLoad = GoldMineObjective.MineGoldPerTrip(gatherer);
                if (gatherer.CarriedResourceType != ResourceType.Gold
                    || gatherer.CarriedAmount <= 0) continue;
                CarriedGold += gatherer.CarriedAmount;
                if (gatherer.State == WorkerState.ReturningToDropOff
                    && gatherer.HasDropOffTarget)
                    GoldInTransit += gatherer.CarriedAmount;
            }

            // Only a currently visible, friendly occupied mine contributes a
            // speculative next load. Hidden memory never refreshes the forecast.
            bool hasCastle = false;
            IReadOnlyList<CastleDropOff> castles = CastleDropOff.Active;
            for (int i = 0; i < castles.Count; i++)
                if (castles[i] != null && castles[i].Faction == faction
                    && castles[i].IsAvailable)
                { hasCastle = true; break; }
            if (!hasCastle || mineLoad <= 0) return;
            IReadOnlyList<GoldMineKnowledgeSnapshot> mines = knowledge.VisibleGoldMines;
            for (int i = 0; i < mines.Count; i++)
            {
                GoldMineKnowledgeSnapshot mine = mines[i];
                if (!mine.HasOwner || mine.Owner != faction
                    || mine.State != GoldMineState.Owned
                    || mine.LastSeenRemainingGold <= 0) continue;
                ExpectedGold += Mathf.Min(mine.LastSeenRemainingGold,
                    Mathf.Clamp(mine.LastSeenOccupants, 0,
                        GoldMineObjective.PawnCapacity) * mineLoad);
            }
        }

        public void DesiredCounts(int workers, out int food, out int wood, out int gold)
        {
            if (workers <= 0) { food = wood = gold = 0; return; }
            float fw = DemandWeight(ResourceType.Food);
            float ww = DemandWeight(ResourceType.Wood);
            float gw = DemandWeight(ResourceType.Gold);
            float total = fw + ww + gw;
            food = Mathf.FloorToInt(workers * fw / total);
            wood = Mathf.FloorToInt(workers * ww / total);
            gold = Mathf.FloorToInt(workers * gw / total);
            while (food + wood + gold < workers)
            {
                float fp = workers * fw / total - food;
                float wp = workers * ww / total - wood;
                float gp = workers * gw / total - gold;
                if (fp >= wp && fp >= gp) food++;
                else if (wp >= gp) wood++;
                else gold++;
            }
        }

        public bool CanSpendAfterBuffers(BuildingData data)
            => data != null && Food - data.FoodCost >= foodBuffer + ReservedFood
                && Wood - data.WoodCost >= woodBuffer + ReservedWood
                && Gold - data.GoldCost >= goldBuffer + ReservedGold;

        public bool CanExecuteReservedPlan()
            => Food >= foodBuffer + ReservedFood
                && Wood >= woodBuffer + ReservedWood
                && Gold >= goldBuffer + ReservedGold;

        private void Add(BuildingData data)
        {
            if (data == null) return;
            ReservedFood += data.FoodCost;
            ReservedWood += data.WoodCost;
            ReservedGold += data.GoldCost;
        }
    }
}
