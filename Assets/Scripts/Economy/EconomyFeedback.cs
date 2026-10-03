using TinyKingdom.Units;
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
                if (pawn != null && pawn.GetComponent<UnitController>()?.IsAlive == true)
                    ResourceGainPopup.Show(pawn.transform.position + Vector3.up * 0.7f,
                        Icon(type), amount);
            };
        }

        public void Watch(CastleDropOff castle)
        {
            castle.Deposited += (type, amount) =>
            {
                if (castle != null)
                    ResourceGainPopup.ShowDeposit(
                        castle.transform.position + Vector3.up * DepositPopupHeight,
                        Icon(type), amount);
            };
        }

        public void Watch(BuildingResourceDropOff tower)
        {
            tower.Deposited += (type, amount) =>
            {
                if (tower != null)
                    ResourceGainPopup.ShowDeposit(
                        tower.transform.position + Vector3.up * DepositPopupHeight,
                        Icon(type), amount);
            };
        }

        public void Show(ResourceType type, int amount, Vector3 position)
        {
            if (amount > 0) ResourceGainPopup.Show(position, Icon(type), amount);
        }

        private Sprite Icon(ResourceType type) => type switch
        {
            ResourceType.Food => foodIcon,
            ResourceType.Wood => woodIcon,
            _ => goldIcon
        };
    }
}
