using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Units;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // One permanent upgrade per defensive building. Purchases use the same
    // authoritative stockpile as construction and production.
    public sealed class BuildingDefenseUpgrade : MonoBehaviour
    {
        private static readonly List<BuildingDefenseUpgrade> RegisteredUpgrades = new();
        public static IReadOnlyList<BuildingDefenseUpgrade> ActiveUpgrades => RegisteredUpgrades;
        public const int CastleCannonWoodCost = 250;
        public const int CastleCannonGoldCost = 200;
        public const int TowerArrowWoodCost = 80;
        public const int TowerArrowGoldCost = 50;
        public const float DefaultCastleUpgradeSeconds = 50f;
        public const float DefaultTowerUpgradeSeconds = 35f;

        [SerializeField] private bool purchased;
        [SerializeField, Min(0.1f)] private float castleUpgradeSeconds = DefaultCastleUpgradeSeconds;
        [SerializeField, Min(0.1f)] private float towerUpgradeSeconds = DefaultTowerUpgradeSeconds;
        [SerializeField] private bool upgrading;
        [SerializeField] private float elapsedSeconds;
        private IDefensiveBuildingHost host;
        private ResourceStockpile stockpile;

        public event Action Changed;

        private void OnEnable()
        {
            if (!RegisteredUpgrades.Contains(this)) RegisteredUpgrades.Add(this);
        }

        private void OnDisable() => RegisteredUpgrades.Remove(this);

        public bool Purchased => purchased;
        public bool IsUpgrading => upgrading;
        public float UpgradeSeconds => host?.Type == BuildingType.Castle
            ? Mathf.Max(0.1f, castleUpgradeSeconds)
            : Mathf.Max(0.1f, towerUpgradeSeconds);
        public float Progress => purchased ? 1f : upgrading
            ? Mathf.Clamp01(elapsedSeconds / UpgradeSeconds) : 0f;
        public float SecondsRemaining => upgrading
            ? Mathf.Max(0f, UpgradeSeconds - elapsedSeconds) : 0f;
        public Faction Faction => host != null ? host.Faction : Faction.Blue;
        public IDefensiveBuildingHost Host => host;
        public int WoodCost => host?.Type == BuildingType.Castle
            ? CastleCannonWoodCost : TowerArrowWoodCost;
        public int GoldCost => host?.Type == BuildingType.Castle
            ? CastleCannonGoldCost : TowerArrowGoldCost;
        public bool CanPurchase => MatchManager.AllowsGameplay && !purchased && !upgrading
            && host != null && host.IsDefenseReady
            && host.Type is BuildingType.Castle or BuildingType.Tower
            && stockpile != null && stockpile.Faction == host.Faction
            && stockpile.CanAfford(0, WoodCost, GoldCost);

        public void Configure(IDefensiveBuildingHost owner, ResourceStockpile resources)
        {
            host = owner;
            stockpile = resources;
        }

        private void Update() => AdvanceUpgrade(Time.deltaTime);

        public void AdvanceUpgrade(float seconds)
        {
            if (!upgrading) return;
            if (host == null || !host.IsDefenseReady)
            {
                // A destroyed host cannot finish its paid upgrade.
                upgrading = false;
                elapsedSeconds = 0f;
                return;
            }
            if (!MatchManager.AllowsGameplay) return;
            elapsedSeconds = Mathf.Min(UpgradeSeconds,
                elapsedSeconds + Mathf.Max(0f, seconds));
            if (elapsedSeconds < UpgradeSeconds) return;
            upgrading = false;
            purchased = true;
            Changed?.Invoke();
        }

        public bool TryPurchase(Faction requester)
        {
            if (requester != Faction || !CanPurchase
                || !stockpile.Spend(0, WoodCost, GoldCost)) return false;
            elapsedSeconds = 0f;
            upgrading = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryCancel(Faction requester)
        {
            if (!MatchManager.AllowsGameplay || !upgrading || purchased
                || requester != Faction || stockpile == null) return false;
            float refundFraction = 1f - Progress;
            upgrading = false;
            elapsedSeconds = 0f;
            stockpile.Add(ResourceType.Wood,
                Mathf.FloorToInt(WoodCost * refundFraction));
            stockpile.Add(ResourceType.Gold,
                Mathf.FloorToInt(GoldCost * refundFraction));
            Changed?.Invoke();
            return true;
        }
    }
}
