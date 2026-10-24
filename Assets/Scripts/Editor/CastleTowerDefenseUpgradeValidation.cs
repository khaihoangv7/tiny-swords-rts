#if UNITY_EDITOR
using System;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class CastleTowerDefenseUpgradeValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Castle Tower Defense Upgrades")]
        public static void Validate()
        {
            Require(BuildingDefense.ArrowCount(BuildingType.Castle, 0, false) == 1
                && BuildingDefense.ArrowCount(BuildingType.Castle, 1, false) == 2
                && BuildingDefense.ArrowCount(BuildingType.Castle, 3, false) == 4,
                "Castle automatic/garrison arrow counts are wrong.");
            Require(BuildingDefense.ArrowCount(BuildingType.Tower, 0, false) == 0
                && BuildingDefense.ArrowCount(BuildingType.Tower, 2, false) == 2
                && BuildingDefense.ArrowCount(BuildingType.Tower, 3, false) == 3
                && BuildingDefense.ArrowCount(BuildingType.Tower, 0, true) == 1
                && BuildingDefense.ArrowCount(BuildingType.Tower, 2, true) == 3
                && BuildingDefense.ArrowCount(BuildingType.Tower, 3, true) == 4,
                "Tower upgrade/garrison arrow counts are wrong.");
            BuildingData castleData = Resources.Load<BuildingData>("Buildings/Castle");
            BuildingData towerData = Resources.Load<BuildingData>("Buildings/Tower");
            Require(castleData != null && towerData != null,
                "Castle or Tower building data is missing.");
            Require(towerData.InternalGarrisonCapacity == 3,
                "Tower cannot hold the three occupants required by its volley rules.");
            Require(BuildingDefense.ArrowDamage(BuildingType.Castle, 0,
                    castleData.DefensiveArrowDamage)
                > BuildingDefense.ArrowDamage(BuildingType.Castle, 1,
                    castleData.DefensiveArrowDamage),
                "Castle automatic arrow must exceed garrison arrow damage.");
            Require(BuildingDefenseUpgrade.CastleCannonWoodCost == 250
                && BuildingDefenseUpgrade.CastleCannonGoldCost == 200
                && BuildingDefenseUpgrade.TowerArrowWoodCost == 80
                && BuildingDefenseUpgrade.TowerArrowGoldCost == 50,
                "Defense upgrade costs are wrong.");
            Require(Mathf.Approximately(
                    BuildingDefenseUpgrade.DefaultCastleUpgradeSeconds, 50f)
                && Mathf.Approximately(
                    BuildingDefenseUpgrade.DefaultTowerUpgradeSeconds, 35f),
                "Castle/Tower upgrade durations must be 50/35 seconds.");
            Require(GoldMineObjective.MaximumGold == 2000
                && GoldMineObjective.PawnCapacity == 3
                && Mathf.Approximately(GoldMineObjective.MineLoadMultiplier, 2.5f)
                && GoldMineObjective.MineGoldPerTrip(20) == 50
                && Mathf.Approximately(GoldMineObjective.MiningSeconds(20, 2, 1.5f), 15f),
                "Gold Mine load or extraction duration is incorrect.");
            Phase6C2AssetCatalog effects = Phase6C2AssetCatalog.Load();
            Require(effects?.Dynamite != null && effects.Dynamite.Length > 0
                && effects.Explosion03 != null && effects.Explosion03.Length > 0,
                "Existing TNT projectile/explosion visuals are missing.");
            if (Application.isPlaying && MatchManager.AllowsGameplay)
            {
                ValidateLivePurchases();
                ValidateStraightProjectiles();
            }
            Debug.Log("Castle/Tower defense structural validation passed. "
                + "Live projectile travel, visibility, impacts and Game View "
                + "presentation require separate Play Mode observation.");
        }

        private static void ValidateLivePurchases()
        {
            CastleDropOff castle = UnityEngine.Object.FindAnyObjectByType<CastleDropOff>();
            Require(castle != null && castle.IsDefenseReady
                && castle.GetComponent<BuildingDefense>() != null
                && castle.GetComponent<BuildingDefenseUpgrade>() != null,
                "Live Castle has not initialized or its defense wiring is missing.");
            TestPurchase(castle, 250, 200);
            foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
            {
                if (site == null || site.Type != BuildingType.Tower
                    || !site.IsDefenseReady) continue;
                Require(site.GetComponent<BuildingDefense>() != null
                    && site.GetComponent<BuildingDefenseUpgrade>() != null,
                    "Live Tower defense/upgrade wiring is missing.");
                TestPurchase(site, 80, 50);
                break;
            }
        }

        private static void TestPurchase(IDefensiveBuildingHost host,
            int woodCost, int goldCost)
        {
            var test = new GameObject("TemporaryDefenseUpgradeValidation");
            try
            {
                ResourceStockpile stockpile = test.AddComponent<ResourceStockpile>();
                stockpile.Configure(host.Faction, 0, woodCost, goldCost);
                BuildingDefenseUpgrade upgrade = test.AddComponent<BuildingDefenseUpgrade>();
                upgrade.Configure(host, stockpile);
                Require(!upgrade.TryPurchase(host.Faction == Faction.Blue
                        ? Faction.Red : Faction.Blue),
                    "Opposing faction purchased a defense upgrade.");
                bool started = upgrade.TryPurchase(host.Faction);
                Require(started && upgrade.IsUpgrading && !upgrade.Purchased
                    && stockpile.GetAmount(ResourceType.Wood) == 0
                    && stockpile.GetAmount(ResourceType.Gold) == 0,
                    $"Defense upgrade start failed: type={host.Type}, ready={host.IsDefenseReady}, "
                    + $"allowed={MatchManager.AllowsGameplay}, started={started}, "
                    + $"wood={stockpile.GetAmount(ResourceType.Wood)}, "
                    + $"gold={stockpile.GetAmount(ResourceType.Gold)}.");
                Require(!upgrade.TryPurchase(host.Faction)
                    && stockpile.GetAmount(ResourceType.Wood) == 0
                    && stockpile.GetAmount(ResourceType.Gold) == 0,
                    "Repeated defense upgrade succeeded or charged twice.");
                upgrade.AdvanceUpgrade(upgrade.UpgradeSeconds * 0.5f);
                Require(upgrade.IsUpgrading && !upgrade.Purchased
                    && upgrade.Progress > 0f && upgrade.Progress < 1f,
                    "Defense upgrade applied before its duration elapsed.");
                Require(upgrade.TryCancel(host.Faction)
                    && !upgrade.TryCancel(host.Faction)
                    && !upgrade.Purchased && !upgrade.IsUpgrading
                    && stockpile.GetAmount(ResourceType.Wood) == woodCost / 2
                    && stockpile.GetAmount(ResourceType.Gold) == goldCost / 2,
                    "Cancel did not return the unused half exactly once.");
                stockpile.Add(ResourceType.Wood, woodCost - woodCost / 2);
                stockpile.Add(ResourceType.Gold, goldCost - goldCost / 2);
                Require(upgrade.TryPurchase(host.Faction)
                    && stockpile.GetAmount(ResourceType.Wood) == 0
                    && stockpile.GetAmount(ResourceType.Gold) == 0,
                    "Re-purchase after cancellation charged incorrectly.");
                upgrade.AdvanceUpgrade(upgrade.UpgradeSeconds);
                Require(!upgrade.IsUpgrading && upgrade.Purchased
                    && Mathf.Approximately(upgrade.Progress, 1f)
                    && !upgrade.TryPurchase(host.Faction),
                    "Defense upgrade did not complete exactly once.");
                BuildingDefenseUpgrade insufficient =
                    test.AddComponent<BuildingDefenseUpgrade>();
                insufficient.Configure(host, stockpile);
                Require(!insufficient.TryPurchase(host.Faction),
                    "Defense purchase succeeded without resources.");
            }
            finally { UnityEngine.Object.Destroy(test); }
        }

        private static void ValidateStraightProjectiles()
        {
            var targetObject = new GameObject("TemporaryArrowTarget");
            var arrowObject = new GameObject("TemporaryStraightArrow");
            var blockerObject = new GameObject("TemporaryArrowBlocker");
            try
            {
                Vector3 origin = new(10000f, 10000f, 0f);
                targetObject.transform.position = origin + Vector3.right * 4f;
                DefenseValidationTarget target =
                    targetObject.AddComponent<DefenseValidationTarget>();
                arrowObject.transform.position = origin;
                CombatProjectile arrow = arrowObject.AddComponent<CombatProjectile>();
                arrow.InitializeStraight(Faction.Blue, target, 5, 16f, 0.3f);
                for (int i = 0; i < 20 && !arrow.IsResolved; i++)
                    arrow.TickProjectile(0.025f);
                Require(arrow.IsResolved && target.Hits == 1,
                    "Stationary target was not hit by one straight arrow.");

                UnityEngine.Object.Destroy(arrowObject);
                arrowObject = new GameObject("TemporaryMissedArrow");
                arrowObject.transform.position = origin;
                targetObject.transform.position = origin + Vector3.right * 4f;
                arrow = arrowObject.AddComponent<CombatProjectile>();
                arrow.InitializeStraight(Faction.Blue, target, 5, 16f, 0.3f);
                targetObject.transform.position += Vector3.up * 2f;
                for (int i = 0; i < 20 && !arrow.IsResolved; i++)
                    arrow.TickProjectile(0.025f);
                Require(arrow.IsResolved && target.Hits == 1
                    && arrowObject.GetComponent<Collider2D>() == null,
                    "Moving target evasion or collider-free miss failed.");

                UnityEngine.Object.Destroy(arrowObject);
                arrowObject = new GameObject("TemporaryBlockedArrow");
                arrowObject.transform.position = origin;
                targetObject.transform.position = origin + Vector3.right * 4f;
                blockerObject.transform.position = origin + Vector3.right * 2f;
                blockerObject.AddComponent<BoxCollider2D>().size = new Vector2(0.3f, 1f);
                Physics2D.SyncTransforms();
                arrow = arrowObject.AddComponent<CombatProjectile>();
                arrow.InitializeStraight(Faction.Blue, target, 5, 16f, 0.3f);
                for (int i = 0; i < 20 && !arrow.IsResolved; i++)
                    arrow.TickProjectile(0.025f);
                Require(arrow.IsResolved && target.Hits == 1
                    && arrowObject.transform.position.x < targetObject.transform.position.x,
                    "Straight arrow crossed a solid blocker.");
            }
            finally
            {
                UnityEngine.Object.Destroy(targetObject);
                UnityEngine.Object.Destroy(arrowObject);
                UnityEngine.Object.Destroy(blockerObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    public sealed class DefenseValidationTarget : MonoBehaviour, ICombatDamageReceiver
    {
        public Faction Faction => Faction.Red;
        public bool IsAlive => true;
        public bool IsTargetable => true;
        public Transform TargetTransform => transform;
        public int Hits { get; private set; }
        public event Action<ICombatDamageReceiver> CombatDied;

        public bool ReceiveAttack(int baseDamage, UnitType attacker,
            AttackDelivery delivery, float sourceMultiplier = 1f,
            bool applyCounterMultiplier = true, UnitController damageSource = null)
        {
            Hits++;
            return true;
        }
    }
}
#endif
