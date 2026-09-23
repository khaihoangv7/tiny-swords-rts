using System;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class FactionPopulation : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Blue;
        [SerializeField, Min(0)] private int startingCap = 10;
        public Faction Faction => faction;
        public int Cap { get; private set; } = 10;
        public int Used { get; private set; }
        public int Reserved { get; private set; }
        public event Action Changed;

        public void Configure(Faction owner, int cap)
        {
            faction = owner;
            startingCap = Mathf.Max(0, cap);
            Cap = startingCap;
            Reserved = 0;
            RefreshUsed();
            Changed?.Invoke();
        }

        private void Update() => RefreshUsed();

        public bool TryReserve(int amount)
        {
            RefreshUsed();
            if (amount <= 0 || Used + Reserved + amount > Cap) return false;
            Reserved += amount;
            Changed?.Invoke();
            return true;
        }

        public void ReleaseReservation(int amount)
        {
            Reserved = Mathf.Max(0, Reserved - Mathf.Max(0, amount));
            RefreshUsed();
            Changed?.Invoke();
        }

        public void RefreshUsed()
        {
            int count = 0;
            foreach (UnitController unit in UnitController.AllUnits)
            {
                if (unit == null || !unit.IsAlive || unit.Faction != faction) continue;
                count += unit.GetComponent<HealthComponent>()?.CombatData?.PopulationCost ?? 0;
            }
            if (count == Used) return;
            Used = count;
            Changed?.Invoke();
        }

        public void AddCap(int amount)
        {
            Cap = Mathf.Max(0, Cap + amount);
            Changed?.Invoke();
        }
    }
}
