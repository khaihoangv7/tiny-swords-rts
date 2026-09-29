using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Editor-only composition reference. Actual entities are made by EconomyTestSetup.
    // The EditorOnly tag strips this hierarchy from builds; Awake hides it in Play Mode.
    public sealed class BattlefieldScenePreview : MonoBehaviour
    {
        private void Awake() => gameObject.SetActive(false);
    }
}
