using System;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // A neutral map building. It reuses combat/Fog/HP presentation without production or population.
    public sealed class GoblinHut : MonoBehaviour, IDefensiveBuildingHost
    {
        private GoblinHutData data;
        private GridManager grid;
        private SpriteRenderer visual;
        private Vector2Int origin;
        private int currentHP;
        private bool occupied;
        public BuildingType Type=>BuildingType.GoblinHut;
        public Faction Faction=>Faction.Goblin;
        public Vector2Int FootprintOrigin=>origin;
        public Vector2Int Footprint=>data.footprint;
        public bool IsProductionReady=>false;
        public BuildingData DefenseData=>null;
        public Transform HostTransform=>transform;
        public SpriteRenderer PresentationRenderer=>visual;
        public Collider2D InteractionCollider=>GetComponent<Collider2D>();
        public float CurrentHP=>currentHP;
        public int MaxHP=>data.maxHP;
        public bool IsAlive=>currentHP>0;
        public bool IsDefenseReady=>IsAlive;
        public float FireThreshold=>BuildingDamagePresentation.FireThreshold;
        public event Action DefenseHostDestroyed;
        public event Action DefenseHostHealthChanged;

        public void Configure(GoblinHutData definition,Vector2Int cell,GridManager navigation)
        {
            data=definition; origin=cell; grid=navigation; currentHP=data.maxHP;
            transform.position=grid.GridToWorld(cell.x,cell.y)+new Vector3(0.5f,0.5f,0);
            var body=new GameObject("VisualRoot"); body.transform.SetParent(transform,false);
            body.transform.localScale=Vector3.one*data.visualScale;
            visual=body.AddComponent<SpriteRenderer>(); visual.sprite=data.intact;
            var collider=gameObject.AddComponent<BoxCollider2D>(); collider.size=data.footprint;
            collider.isTrigger=true;
            for(int y=0;y<Footprint.y;y++) for(int x=0;x<Footprint.x;x++) grid.SetOccupied(cell.x+x,cell.y+y,true);
            occupied=true;
            GroundSorting.Ensure(gameObject,false,0f);
            gameObject.AddComponent<BuildingCombatTarget>().Configure(this);
            gameObject.AddComponent<BuildingVisionSource>().Configure(this);
            gameObject.AddComponent<BuildingHealthBar>().Configure(this,visual.bounds.size.y+0.2f);
            gameObject.AddComponent<BuildingDamagePresentation>().Configure(this);
            var gate=GetComponent<FogPresentationGate>()??gameObject.AddComponent<FogPresentationGate>();
            gate.ConfigureInitialVisibility(true);
        }

        public bool ReceiveBuildingDamage(int damage)
        {
            if(!MatchManager.AllowsGameplay || !IsAlive || damage<=0) return false;
            currentHP=Mathf.Max(0,currentHP-damage);
            DefenseHostHealthChanged?.Invoke();
            if(currentHP==0)
            {
                ReleaseOccupancy(); visual.sprite=data.wreck;
                DefenseHostDestroyed?.Invoke();
            }
            return true;
        }
        public void ReleaseOccupancy()
        {
            if(!occupied || grid==null) return;
            for(int y=0;y<Footprint.y;y++) for(int x=0;x<Footprint.x;x++) grid.SetOccupied(origin.x+x,origin.y+y,false);
            occupied=false;
        }
        private void OnDestroy()=>ReleaseOccupancy();
    }
}
