using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class UnitProductionQueue : MonoBehaviour
    {
        private const int MaximumQueue = 5;
        private readonly Queue<UnitType> requests = new();
        private readonly Dictionary<UnitType, UnitCombatData> definitions = new();
        private IProductionHost host;
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private GridManager grid;
        private Func<UnitType, Vector3, UnitController> spawn;
        private float elapsed;

        public int Count => requests.Count;
        public int MaxCount => MaximumQueue;
        public UnitType? Current => requests.Count > 0 ? requests.Peek() : null;
        public float Progress => Current.HasValue && definitions.TryGetValue(Current.Value,
            out UnitCombatData data) ? Mathf.Clamp01(elapsed / data.TrainingSeconds) : 0f;
        public event Action Changed;

        public void Configure(IProductionHost producer, ResourceStockpile resources,
            FactionPopulation cap, GridManager navigation, IEnumerable<UnitCombatData> units,
            Func<UnitType, Vector3, UnitController> spawnUnit)
        {
            host = producer;
            stockpile = resources;
            population = cap;
            grid = navigation;
            spawn = spawnUnit;
            definitions.Clear();
            foreach (UnitCombatData data in units)
                if (data != null) definitions[data.UnitType] = data;
        }

        public static bool CanProduce(BuildingType building, UnitType unit)
            => building switch
            {
                BuildingType.Castle => unit == UnitType.Pawn,
                BuildingType.Barracks => unit == UnitType.Warrior || unit == UnitType.Lancer,
                BuildingType.Archery => unit == UnitType.Archer,
                BuildingType.Monastery => unit == UnitType.Monk,
                _ => false
            };

        public UnitCombatData GetData(UnitType type)
            => definitions.TryGetValue(type, out UnitCombatData data) ? data : null;

        public bool CanTrain(UnitType type)
        {
            UnitCombatData data = GetData(type);
            return MatchManager.AllowsGameplay && host != null
                && host.IsProductionReady && data != null
                && CanProduce(host.Type, type) && requests.Count < MaximumQueue
                && stockpile != null && population != null
                && stockpile.CanAfford(data.FoodCost, data.WoodCost, data.GoldCost)
                && population.Used + population.Reserved + data.PopulationCost <= population.Cap;
        }

        // Gameplay API shared by UI and future commanders. Payment and population
        // reservation happen exactly once, when the request enters the FIFO.
        public bool TryTrain(UnitType type)
        {
            if (!CanTrain(type)) return false;
            UnitCombatData data = GetData(type);
            if (!population.TryReserve(data.PopulationCost)) return false;
            if (!stockpile.Spend(data.FoodCost, data.WoodCost, data.GoldCost))
            {
                population.ReleaseReservation(data.PopulationCost);
                return false;
            }
            requests.Enqueue(type);
            Changed?.Invoke();
            return true;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || requests.Count == 0
                || host == null || !host.IsProductionReady) return;
            UnitCombatData data = definitions[requests.Peek()];
            elapsed = Mathf.Min(data.TrainingSeconds, elapsed + Time.deltaTime);
            if (elapsed < data.TrainingSeconds) return;
            population.RefreshUsed();
            if (population.Used + population.Reserved > population.Cap
                || !BuildingSpawnPoint.TryFind(host, grid, out Vector3 position)) return;
            UnitController unit = spawn?.Invoke(requests.Peek(), position);
            if (unit == null) return;
            requests.Dequeue();
            elapsed = 0f;
            population.ReleaseReservation(data.PopulationCost);
            if (host is Component component)
                component.GetComponent<BuildingRallyPoint>()?.IssueRallyMove(unit);
            Changed?.Invoke();
        }

        public string QueueSummary()
            => requests.Count == 0 ? "Empty" : string.Join(" > ", requests);

        public void CopyQueue(List<UnitType> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (UnitType type in requests) destination.Add(type);
        }

        private void OnDestroy()
        {
            foreach (UnitType type in requests)
                if (definitions.TryGetValue(type, out UnitCombatData data))
                    population?.ReleaseReservation(data.PopulationCost);
            requests.Clear();
        }
    }
}
