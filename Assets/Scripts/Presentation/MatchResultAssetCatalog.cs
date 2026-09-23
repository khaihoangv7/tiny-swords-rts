using System;
using UnityEngine;

namespace TinyKingdom.Presentation
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Match Result Asset Catalog")]
    public sealed class MatchResultAssetCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] victoryRibbon = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] defeatRibbon = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] resultPanel = Array.Empty<Sprite>();
        [SerializeField] private Sprite victoryButtonRegular;
        [SerializeField] private Sprite victoryButtonPressed;
        [SerializeField] private Sprite defeatButtonRegular;
        [SerializeField] private Sprite defeatButtonPressed;

        public Sprite[] VictoryRibbon => victoryRibbon;
        public Sprite[] DefeatRibbon => defeatRibbon;
        public Sprite[] ResultPanel => resultPanel;
        public Sprite VictoryButtonRegular => victoryButtonRegular;
        public Sprite VictoryButtonPressed => victoryButtonPressed;
        public Sprite DefeatButtonRegular => defeatButtonRegular;
        public Sprite DefeatButtonPressed => defeatButtonPressed;
        public bool IsComplete => victoryRibbon?.Length == 3 && defeatRibbon?.Length == 3
            && resultPanel?.Length == 9 && victoryButtonRegular != null
            && victoryButtonPressed != null && defeatButtonRegular != null
            && defeatButtonPressed != null;

        public static MatchResultAssetCatalog Load()
            => Resources.Load<MatchResultAssetCatalog>(
                "Presentation/Phase6CMatchResultPresentation");

#if UNITY_EDITOR
        public void SetData(Sprite[] victory, Sprite[] defeat, Sprite[] panel,
            Sprite blueRegular, Sprite bluePressed, Sprite redRegular, Sprite redPressed)
        {
            victoryRibbon = victory ?? Array.Empty<Sprite>();
            defeatRibbon = defeat ?? Array.Empty<Sprite>();
            resultPanel = panel ?? Array.Empty<Sprite>();
            victoryButtonRegular = blueRegular;
            victoryButtonPressed = bluePressed;
            defeatButtonRegular = redRegular;
            defeatButtonPressed = redPressed;
        }
#endif
    }
}
