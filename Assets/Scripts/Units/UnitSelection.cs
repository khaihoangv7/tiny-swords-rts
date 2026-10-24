using System.Collections.Generic;
using System;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class UnitSelection : MonoBehaviour
    {
        private static readonly List<UnitSelection> RegisteredUnits = new();

        [SerializeField, Min(1f)] private float clickRadiusPixels = 28f;
        [SerializeField, Min(0.05f)] private float markerWidth = 0.56f;
        [SerializeField, Min(0.05f)] private float markerHeight = 0.20f;
        [SerializeField] private float markerYOffset = -0.43f;

        private UnitController unitController;
        private UnitMovement unitMovement;
        private SelectionCursorMarker selectionIndicator;

        public static IReadOnlyList<UnitSelection> ActiveUnits => RegisteredUnits;
        public UnitController Controller => unitController != null ? unitController : unitController = GetComponent<UnitController>();
        public UnitMovement Movement => unitMovement != null ? unitMovement : unitMovement = GetComponent<UnitMovement>();
        public float ClickRadiusPixels => clickRadiusPixels;
        public float MarkerYOffset => markerYOffset * UnitVisualPresentation.Scale;
        public bool IsSelected { get; private set; }
        public bool IsWorldSelectable { get; private set; } = true;
        public event Action<UnitSelection> BecameUnavailable;

        private void Awake()
        {
            unitController = GetComponent<UnitController>();
            unitMovement = GetComponent<UnitMovement>();
            CreateSelectionIndicator();
            SetSelected(false);
        }

        private void OnEnable()
        {
            unitController ??= GetComponent<UnitController>();
            unitController.StateChanged += HandleStateChanged;
            if (unitController.IsAlive && IsWorldSelectable && !RegisteredUnits.Contains(this))
                RegisteredUnits.Add(this);
        }

        private void OnDisable()
        {
            if (unitController != null)
                unitController.StateChanged -= HandleStateChanged;
            RegisteredUnits.Remove(this);
            BecameUnavailable?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            selected = selected && Controller.IsAlive && IsWorldSelectable;
            IsSelected = selected;
            if (selectionIndicator != null)
            {
                if (!selectionIndicator.IsConfigured) RefreshIndicatorGeometry();
                selectionIndicator.SetSelected(selected);
            }
        }

        public void SetWorldSelectable(bool value)
        {
            IsWorldSelectable = value;
            if (!value)
            {
                SetSelected(false);
                RegisteredUnits.Remove(this);
                BecameUnavailable?.Invoke(this);
            }
            else if (isActiveAndEnabled && Controller.IsAlive && !RegisteredUnits.Contains(this))
                RegisteredUnits.Add(this);
        }

        public void ConfigureMarkerYOffset(float offset)
        {
            markerYOffset = offset;
            RefreshIndicatorGeometry();
        }

        private void HandleStateChanged(UnitState state)
        {
            if (state != UnitState.Dead)
                return;
            SetSelected(false);
            RegisteredUnits.Remove(this);
            BecameUnavailable?.Invoke(this);
        }

        private void CreateSelectionIndicator()
        {
            selectionIndicator = GetComponent<SelectionCursorMarker>()
                ?? gameObject.AddComponent<SelectionCursorMarker>();
            RefreshIndicatorGeometry();
        }

        private void RefreshIndicatorGeometry()
        {
            if (selectionIndicator == null) return;
            float scale = UnitVisualPresentation.Scale;
            selectionIndicator.Configure(markerWidth * scale,
                Mathf.Max(0.48f, markerHeight * scale), markerYOffset * scale,
                GetComponent<SpriteRenderer>(), 0.8f);
        }
    }
}
