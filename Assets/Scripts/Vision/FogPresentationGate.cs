using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // forceRenderingOff preserves animation/UI scripts' own Renderer.enabled state.
    public sealed class FogPresentationGate : MonoBehaviour
    {
        [SerializeField] private bool hideUntilFirstVisibilityEvaluation;
        private readonly List<Renderer> renderers = new();
        private bool cacheDirty = true;
        public bool IsPresented { get; private set; } = true;
        public bool HidesUntilFirstVisibilityEvaluation => hideUntilFirstVisibilityEvaluation;

        private void Awake()
        {
            if (hideUntilFirstVisibilityEvaluation)
                SetPresented(false);
        }

        public void ConfigureInitialVisibility(bool hideInitially)
            => hideUntilFirstVisibilityEvaluation = hideInitially;

        public void SetPresented(bool value)
        {
            if (IsPresented == value && !cacheDirty) return;
            IsPresented = value;
            renderers.Clear();
            GetComponentsInChildren(true, renderers);
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                Renderer item = renderers[i];
                if (item == null) renderers.RemoveAt(i);
                else item.forceRenderingOff = !value;
            }
            cacheDirty = false;
        }

        private void OnTransformChildrenChanged()
        {
            cacheDirty = true;
            if (!IsPresented) SetPresented(false);
        }

        private void OnDisable()
        {
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].forceRenderingOff = false;
            cacheDirty = true;
        }
    }
}
