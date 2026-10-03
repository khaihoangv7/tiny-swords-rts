using System.Collections.Generic;
using System.Globalization;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.UI
{
    internal sealed class CommandTooltipData
    {
        internal string Title;
        internal string Stats;
        internal string Details;
        internal string Hotkey;
        internal int FoodCost;
        internal int WoodCost;
        internal int GoldCost;
        internal int PopulationCost;
    }

    internal static class CommandTooltipDataBuilder
    {
        private static readonly UnitType[] CombatTypes =
        {
            UnitType.Warrior, UnitType.Archer, UnitType.Lancer
        };

        internal static CommandTooltipData ForUnit(UnitCombatData data)
        {
            if (data == null) return null;
            var tooltip = new CommandTooltipData
            {
                Title = data.UnitType.ToString().ToUpperInvariant(),
                FoodCost = data.FoodCost,
                WoodCost = data.WoodCost,
                GoldCost = data.GoldCost,
                PopulationCost = data.PopulationCost
            };

            switch (data.UnitType)
            {
                case UnitType.Monk:
                    tooltip.Stats = $"HP {data.MaxHP}    Speed {Number(data.MoveSpeed)}    "
                        + $"Train {Number(data.TrainingSeconds)}s\nHeal {MonkHealer.HealAmount}    "
                        + $"Range {Number(MonkHealer.HealRange)}    "
                        + $"Interval {Number(MonkHealer.HealInterval)}s";
                    tooltip.Details = "Role: Support / Healer\nCannot attack. Heals damaged friendly units.";
                    break;
                case UnitType.Pawn:
                    tooltip.Stats = $"HP {data.MaxHP}    Speed {Number(data.MoveSpeed)}    "
                        + $"Train {Number(data.TrainingSeconds)}s";
                    tooltip.Details = "Role: Worker\nGathers Food, Wood and Gold. Constructs and repairs buildings.";
                    break;
                default:
                    tooltip.Stats = $"HP {data.MaxHP}    Damage {data.BaseDamage}    "
                        + $"Range {Number(data.AttackRange)}\nSpeed {Number(data.MoveSpeed)}    "
                        + $"Attack {Number(data.AttackInterval)}s    "
                        + $"Train {Number(data.TrainingSeconds)}s";
                    tooltip.Details = MilitaryRole(data.UnitType) + Matchups(data.UnitType);
                    break;
            }
            return tooltip;
        }

        internal static CommandTooltipData ForBuilding(BuildingData data, string displayName)
        {
            if (data == null) return null;
            return new CommandTooltipData
            {
                Title = displayName.ToUpperInvariant(),
                FoodCost = data.FoodCost,
                WoodCost = data.WoodCost,
                GoldCost = data.GoldCost,
                Stats = $"HP {data.MaxHP}    Size {data.Footprint.x} x {data.Footprint.y}    "
                    + $"Build {Number(data.ConstructionSeconds)}s",
                Details = BuildingPurpose(data)
            };
        }

        internal static CommandTooltipData Simple(string title, string details, string hotkey = "")
            => new()
            {
                Title = title.ToUpperInvariant(),
                Details = details,
                Hotkey = hotkey
            };

        internal static CommandTooltipData Scout(string details)
            => new()
            {
                Title = "SCOUT",
                WoodCost = PawnScoutPromotion.WoodCost,
                Details = details
            };

        private static string MilitaryRole(UnitType type)
        {
            string role = type switch
            {
                UnitType.Warrior => "Role: Heavy melee",
                UnitType.Archer => "Role: Ranged damage",
                UnitType.Lancer => "Role: Mobile melee",
                _ => "Role: Military"
            };
            return role;
        }

        private static string Matchups(UnitType type)
        {
            var strong = new List<string>(2);
            var weak = new List<string>(2);
            foreach (UnitType other in CombatTypes)
            {
                if (other == type) continue;
                if (CombatDamage.Multiplier(type, other) > 1.0001f)
                    strong.Add(other.ToString());
                if (CombatDamage.Multiplier(other, type) > 1.0001f)
                    weak.Add(other.ToString());
            }

            string value = strong.Count > 0
                ? "\nStrong against: " + string.Join(", ", strong)
                : "";
            if (weak.Count > 0)
                value += "\nWeak against: " + string.Join(", ", weak);
            return value;
        }

        private static string BuildingPurpose(BuildingData data)
        {
            switch (data.Type)
            {
                case BuildingType.House:
                    return $"Purpose: Population support\nProvides +{data.PopulationBonus} Population Capacity when completed.";
                case BuildingType.Barracks:
                case BuildingType.Archery:
                case BuildingType.Monastery:
                    return "Purpose: Unit production\nTrains: " + ProductionRoster(data.Type);
                case BuildingType.Tower:
                    return $"Purpose: Defensive garrison\nInternal capacity {data.InternalGarrisonCapacity}; "
                        + $"mounted Archers {data.MountedArcherCapacity}.\n"
                        + $"Occupants fire {data.DefensiveArrowDamage}-damage arrows every "
                        + $"{Number(data.DefensiveVolleyInterval)}s at range "
                        + $"{Number(data.DefensiveAttackRange)}.";
                default:
                    return "Purpose: Building";
            }
        }

        private static string ProductionRoster(BuildingType building)
        {
            var names = new List<string>(3);
            foreach (UnitType type in CombatTypes)
                if (UnitProductionQueue.CanProduce(building, type)) names.Add(type.ToString());
            if (UnitProductionQueue.CanProduce(building, UnitType.Monk)) names.Add("Monk");
            if (UnitProductionQueue.CanProduce(building, UnitType.Pawn)) names.Add("Pawn");
            return names.Count > 0 ? string.Join(" / ", names) : "None";
        }

        private static string Number(float value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
