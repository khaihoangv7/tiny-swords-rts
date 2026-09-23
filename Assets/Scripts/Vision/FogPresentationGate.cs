using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // forceRenderingOff preserves animation/UI scripts' own Renderer.enabled state.
    public sealed class FogPresentationGate : MonoBehaviour
    {
        private readonly List<Renderer> renderers = new();
        public bool IsPresented { get; private set; } = true;

        public void SetPresented(bool value)
        {
            IsPresented = value;
            renderers.Clear();
            GetComponentsInChildren(true, renderers);
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                Renderer item = renderers[i];
                if (item == null) renderers.RemoveAt(i);
                else item.forceRenderingOff = !value;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = false;
        }
    }
}
