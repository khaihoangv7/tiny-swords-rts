using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Schema 1: 16 global facts, five 4-value own-unit groups, six
    // 4-value own-building groups, five visible-enemy-unit groups, and four
    // remembered-enemy-building groups. See RL_SPEC.md for every index.
    public sealed class RlObservationProvider
    {
        public const int SchemaVersion = 1;
        public const int FeatureCount = 96;
        private static readonly UnitType[] UnitTypes =
        {
            UnitType.Pawn, UnitType.Warrior, UnitType.Archer,
            UnitType.Lancer, UnitType.Monk
        };
        private static readonly BuildingType[] OwnBuildingTypes =
        {
            BuildingType.Castle, BuildingType.House, BuildingType.Barracks,
            BuildingType.Archery, BuildingType.Monastery, BuildingType.Tower
        };
        private static readonly BuildingType[] KnownEnemyBuildingTypes =
        {
            BuildingType.Castle, BuildingType.Barracks,
            BuildingType.Archery, BuildingType.Tower
        };

        public bool TryBuild(FactionKnowledge knowledge, MatchManager match,
            float[] destination)
        {
            if (knowledge == null || !knowledge.IsReady || match == null
                || destination == null || destination.Length != FeatureCount)
                return false;
            Array.Clear(destination, 0, destination.Length);
            ResourceStockpile stock = knowledge.Stockpile;
            FactionPopulation pop = knowledge.Population;
            GridManager grid = knowledge.TeamVision.GridManager;
            if (stock == null || pop == null || grid == null) return false;

            destination[0] = Norm(stock.GetAmount(ResourceType.Food), 1000f);
            destination[1] = Norm(stock.GetAmount(ResourceType.Wood), 1000f);
            destination[2] = Norm(stock.GetAmount(ResourceType.Gold), 1000f);
            destination[3] = Norm(pop.Used, 50f);
            destination[4] = Norm(pop.Cap, 50f);
            destination[5] = Norm(pop.Reserved, 50f);
            destination[6] = Norm(Mathf.Max(0, pop.Cap - pop.Used - pop.Reserved), 50f);
            destination[7] = knowledge.TotalCellCount > 0
                ? Norm(knowledge.ExploredCellCount, knowledge.TotalCellCount) : 0f;
            destination[8] = Norm(match.ElapsedGameplaySeconds, 1800f);
            destination[9] = Norm(knowledge.OwnUnits.Count, 50f);
            destination[10] = Norm(knowledge.OwnBuildings.Count, 30f);
            destination[11] = Norm(knowledge.VisibleEnemyUnits.Count, 50f);
            destination[12] = Norm(knowledge.KnownResources.Count, 100f);
            destination[13] = Norm(knowledge.KnownGoldMines.Count, 8f);
            destination[14] = Norm(knowledge.KnownGoblins.Count, 20f);
            destination[15] = Norm(knowledge.LastMilitaryObservation.TotalCount, 50f);

            for (int group = 0; group < UnitTypes.Length; group++)
                WriteOwnUnitGroup(knowledge.OwnUnits, UnitTypes[group], grid,
                    destination, 16 + group * 4);
            for (int group = 0; group < OwnBuildingTypes.Length; group++)
                WriteOwnBuildingGroup(knowledge.OwnBuildings,
                    OwnBuildingTypes[group], grid, destination, 36 + group * 4);
            for (int group = 0; group < UnitTypes.Length; group++)
                WriteVisibleEnemyGroup(knowledge.VisibleEnemyUnits,
                    UnitTypes[group], grid, destination, 60 + group * 4);
            for (int group = 0; group < KnownEnemyBuildingTypes.Length; group++)
                WriteKnownBuildingGroup(knowledge.KnownEnemyBuildings,
                    KnownEnemyBuildingTypes[group], grid, destination, 80 + group * 4);

            for (int i = 0; i < destination.Length; i++)
                if (float.IsNaN(destination[i]) || float.IsInfinity(destination[i])
                    || destination[i] < 0f || destination[i] > 1f) return false;
            return true;
        }

        private static void WriteOwnUnitGroup(IReadOnlyList<UnitController> units,
            UnitType type, GridManager grid, float[] output, int index)
        {
            int count = 0;
            float hp = 0f, x = 0f, y = 0f;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                HealthComponent health = unit != null
                    ? unit.GetComponent<HealthComponent>() : null;
                if (unit == null || !unit.IsAlive || health?.CombatData == null
                    || health.CombatData.UnitType != type) continue;
                count++;
                hp += Norm(health.CurrentHP, health.MaxHP);
                Vector2Int cell = grid.WorldToGrid(unit.transform.position);
                x += Norm(cell.x, grid.Width - 1);
                y += Norm(cell.y, grid.Height - 1);
            }
            WriteGroup(output, index, count, 50f, hp, x, y);
        }

        private static void WriteOwnBuildingGroup(
            IReadOnlyList<IDefensiveBuildingHost> buildings, BuildingType type,
            GridManager grid, float[] output, int index)
        {
            int count = 0;
            float hp = 0f, x = 0f, y = 0f;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost building = buildings[i];
                if (building == null || !building.IsAlive || building.Type != type)
                    continue;
                count++;
                hp += Norm(building.CurrentHP, building.MaxHP);
                Vector2Int cell = grid.WorldToGrid(building.HostTransform.position);
                x += Norm(cell.x, grid.Width - 1);
                y += Norm(cell.y, grid.Height - 1);
            }
            WriteGroup(output, index, count, 30f, hp, x, y);
        }

        private static void WriteVisibleEnemyGroup(
            IReadOnlyList<VisibleUnitObservation> units, UnitType type,
            GridManager grid, float[] output, int index)
        {
            int count = 0;
            float hp = 0f, x = 0f, y = 0f;
            for (int i = 0; i < units.Count; i++)
            {
                VisibleUnitObservation unit = units[i];
                if (unit.UnitType != type) continue;
                count++;
                hp += Norm(unit.CurrentHP, unit.MaxHP);
                Vector2Int cell = grid.WorldToGrid(unit.Position);
                x += Norm(cell.x, grid.Width - 1);
                y += Norm(cell.y, grid.Height - 1);
            }
            WriteGroup(output, index, count, 50f, hp, x, y);
        }

        private static void WriteKnownBuildingGroup(
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings, BuildingType type,
            GridManager grid, float[] output, int index)
        {
            int count = 0;
            float hp = 0f, x = 0f, y = 0f;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingKnowledgeSnapshot building = buildings[i];
                if (building.BuildingType != type) continue;
                count++;
                hp += Mathf.Clamp01(building.LastSeenHealthRatio);
                Vector2Int cell = grid.WorldToGrid(building.LastSeenPosition);
                x += Norm(cell.x, grid.Width - 1);
                y += Norm(cell.y, grid.Height - 1);
            }
            WriteGroup(output, index, count, 30f, hp, x, y);
        }

        private static void WriteGroup(float[] output, int index, int count,
            float countLimit, float hpSum, float xSum, float ySum)
        {
            output[index] = Norm(count, countLimit);
            output[index + 1] = count > 0 ? Mathf.Clamp01(hpSum / count) : 0f;
            output[index + 2] = count > 0 ? Mathf.Clamp01(xSum / count) : 0f;
            output[index + 3] = count > 0 ? Mathf.Clamp01(ySum / count) : 0f;
        }

        private static float Norm(float value, float maximum)
            => maximum > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Clamp01(value / maximum) : 0f;
    }
}
