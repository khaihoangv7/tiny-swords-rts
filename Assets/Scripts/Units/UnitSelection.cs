using System.Collections.Generic;
using System;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class UnitSelection : MonoBehaviour
    {
        private static readonly List<UnitSelection> RegisteredUnits = new();

        [SerializeField, Min(1f)] private float clickRadiusPixels = 28f;
        [SerializeField, Min(0.1f)] private float indicatorRadius = 0.55f;

        private UnitController unitController;
        private UnitMovement unitMovement;
        private LineRenderer selectionIndicator;
        private Material indicatorMaterial;

        public static IReadOnlyList<UnitSelection> ActiveUnits => RegisteredUnits;
        public UnitController Controller => unitController != null ? unitController : unitController = GetComponent<UnitController>();
        public UnitMovement Movement => unitMovement != null ? unitMovement : unitMovement = GetComponent<UnitMovement>();
        public float ClickRadiusPixels => clickRadiusPixels;
        public bool IsSelected { get; private set; }
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
            if (unitController.IsAlive && !RegisteredUnits.Contains(this))
                RegisteredUnits.Add(this);
        }

        private void OnDisable()
        {
            if (unitController != null)
                unitController.StateChanged -= HandleStateChanged;
            RegisteredUnits.Remove(this);
            BecameUnavailable?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (indicatorMaterial != null)
                Destroy(indicatorMaterial);
        }

        public void SetSelected(bool selected)
        {
            selected = selected && Controller.IsAlive;
            IsSelected = selected;
            if (selectionIndicator != null)
                selectionIndicator.enabled = selected;
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
            var indicatorObject = new GameObject("SelectionIndicator");
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            selectionIndicator = indicatorObject.AddComponent<LineRenderer>();
            selectionIndicator.useWorldSpace = false;
            selectionIndicator.loop = true;
            selectionIndicator.positionCount = 40;
            selectionIndicator.startWidth = 0.045f;
            selectionIndicator.endWidth = 0.045f;
            selectionIndicator.numCornerVertices = 2;
            selectionIndicator.sortingOrder = 20;
            indicatorMaterial = new Material(Shader.Find("Sprites/Default"));
            indicatorMaterial.color = new Color(0.2f, 1f, 0.25f, 0.95f);
            selectionIndicator.sharedMaterial = indicatorMaterial;

            for (int i = 0; i < selectionIndicator.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / selectionIndicator.positionCount;
                selectionIndicator.SetPosition(i,
                    new Vector3(Mathf.Cos(angle) * indicatorRadius, Mathf.Sin(angle) * indicatorRadius * 0.55f, 0f));
            }
        }
    }
}
