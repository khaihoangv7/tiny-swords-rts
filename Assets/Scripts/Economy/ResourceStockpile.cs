using System;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public sealed class ResourceStockpile : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Blue;
        [SerializeField, Min(0)] private int food = 200;
        [SerializeField, Min(0)] private int wood = 200;
        [SerializeField, Min(0)] private int gold = 100;

        public Faction Faction => faction;
        public event Action Changed;

        public void Configure(Faction owner, int startingFood, int startingWood, int startingGold)
        {
            faction = owner;
            food = Mathf.Max(0, startingFood);
            wood = Mathf.Max(0, startingWood);
            gold = Mathf.Max(0, startingGold);
            Changed?.Invoke();
        }

        public int GetAmount(ResourceType type) => type switch
        {
            ResourceType.Food => food,
            ResourceType.Wood => wood,
            ResourceType.Gold => gold,
            _ => 0
        };

        public bool CanAfford(int foodCost, int woodCost, int goldCost)
            => foodCost >= 0 && woodCost >= 0 && goldCost >= 0
                && food >= foodCost && wood >= woodCost && gold >= goldCost;

        public bool Spend(int foodCost, int woodCost, int goldCost)
        {
            if (!CanAfford(foodCost, woodCost, goldCost)) return false;
            food -= foodCost;
            wood -= woodCost;
            gold -= goldCost;
            Changed?.Invoke();
            return true;
        }

        public void Add(ResourceType type, int amount)
        {
            if (amount <= 0) return;
            switch (type)
            {
                case ResourceType.Food: food = checked(food + amount); break;
                case ResourceType.Wood: wood = checked(wood + amount); break;
                case ResourceType.Gold: gold = checked(gold + amount); break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            Changed?.Invoke();
        }
    }
}
