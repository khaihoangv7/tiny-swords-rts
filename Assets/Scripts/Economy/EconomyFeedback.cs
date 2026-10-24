using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public sealed class EconomyFeedback : MonoBehaviour
    {
        public const float DepositPopupHeight = 1.9f;

        [SerializeField] private Sprite foodIcon;
        [SerializeField] private Sprite woodIcon;
        [SerializeField] private Sprite goldIcon;

        public void Configure(Sprite food, Sprite wood, Sprite gold)
        {
            foodIcon = food;
            woodIcon = wood;
            goldIcon = gold;
        }

        public void Watch(PawnGatherer pawn)
        {
            pawn.ResourceGathered += (type, amount) =>
            {
                UnitController unit = pawn != null
                    ? pawn.GetComponent<UnitController>() : null;
                if (unit != null && unit.IsAlive
                    && unit.Faction == PlayerViewRelation.PlayerViewFaction)
                    ResourceGainPopup.Show(pawn.transform.position + Vector3.up * 0.7f,
                        Icon(type), amount);
            };
        }

        public void Watch(CastleDropOff castle)
        {
            castle.Deposited += (type, amount) =>
            {
                if (castle != null
                    && castle.Faction == PlayerViewRelation.PlayerViewFaction)
                    ResourceGainPopup.ShowDeposit(
                        castle.transform.position + Vector3.up * DepositPopupHeight,
                        Icon(type), amount);
            };
        }

        public void Watch(BuildingResourceDropOff tower)
        {
            tower.Deposited += (type, amount) =>
            {
                if (tower != null
                    && tower.Faction == PlayerViewRelation.PlayerViewFaction)
                    ResourceGainPopup.ShowDeposit(
                        tower.transform.position + Vector3.up * DepositPopupHeight,
                        Icon(type), amount);
            };
        }

        public void Show(ResourceType type, int amount, Vector3 position,
            Faction owner)
        {
            if (amount > 0 && owner == PlayerViewRelation.PlayerViewFaction)
                ResourceGainPopup.Show(position, Icon(type), amount);
        }

        private Sprite Icon(ResourceType type) => type switch
        {
            ResourceType.Food => foodIcon,
            ResourceType.Wood => woodIcon,
            _ => goldIcon
        };
    }
}
