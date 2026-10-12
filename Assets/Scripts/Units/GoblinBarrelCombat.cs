using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController),typeof(UnitMovement),typeof(HealthComponent))]
    public sealed class GoblinBarrelCombat : MonoBehaviour
    {
        [SerializeField] private GoblinBarrelData data;
        private enum State { Hidden, Emerging, Chasing, Returning, Withdrawing, Armed }
        private State state;
        private UnitController unit;
        private UnitMovement movement;
        private HealthComponent health;
        private UnitAnimator animator;
        private HealthComponent target;
        private Vector3 home;
        private float timer;
        private float scan;
        private float repath;
        public bool IsArmed => state==State.Armed && unit.IsAlive;
        public void Configure(GoblinBarrelData value) => data=value;

        private void Awake()
        {
            unit=GetComponent<UnitController>(); movement=GetComponent<UnitMovement>();
            health=GetComponent<HealthComponent>(); animator=GetComponent<UnitAnimator>();
            home=transform.position;
        }

        private void Update()
        {
            if (data==null || !unit.IsAlive) return;
            if (!MatchManager.AllowsGameplay)
            { target=null; timer=0; state=State.Hidden; if(movement.IsMoving) movement.Stop(); return; }
            float dt=Time.deltaTime; scan-=dt; repath-=dt;
            if (state is State.Emerging or State.Withdrawing or State.Armed)
            {
                timer-=dt;
                if(timer>0) return;
                if(state==State.Armed) { Detonate(); return; }
                state=state==State.Emerging?State.Chasing:State.Hidden;
                unit.SetState(UnitState.Idle);
            }
            if (!Valid(target)) target=null;
            if(target==null && scan<=0)
            { scan=0.25f; target=FindTarget(); }
            if(target!=null)
            {
                if(state==State.Hidden)
                {
                    state=State.Emerging; timer=data.emergeSeconds;
                    unit.SetState(UnitState.Attacking); animator?.PlaySequence(data.emerge); return;
                }
                state=State.Chasing;
                if(Vector2.Distance(transform.position,target.transform.position)<=data.fuseDistance)
                {
                    movement.Stop(); state=State.Armed; timer=data.fuseSeconds;
                    unit.SetState(UnitState.Attacking);
                    animator?.PlayAction(target.transform.position-transform.position); return;
                }
                if(repath<=0)
                { movement.TryIssueChase(target.transform.position); repath=0.3f; }
                return;
            }
            if(Vector2.Distance(transform.position,home)>0.2f)
            {
                state=State.Returning;
                if(repath<=0) { movement.TryIssueChase(home); repath=0.3f; }
            }
            else if(state!=State.Hidden)
            {
                movement.Stop(); state=State.Withdrawing; timer=data.withdrawSeconds;
                unit.SetState(UnitState.Attacking); animator?.PlaySequence(data.withdraw);
            }
        }

        private bool Valid(HealthComponent candidate)
        {
            if(candidate==null || !candidate.IsTargetable) return false;
            var actor=candidate.GetComponent<UnitController>();
            if(actor.Faction is not (Faction.Blue or Faction.Red)
                || Vector2.Distance(candidate.transform.position,home)>data.homeLeash) return false;
            GridManager grid=movement.GridManager;
            return grid==null || GroundSorting.ElevationAt(grid,transform.position)
                ==GroundSorting.ElevationAt(grid,candidate.transform.position);
        }

        private HealthComponent FindTarget()
        {
            HealthComponent best=null; float closest=data.detectionRadius*data.detectionRadius;
            foreach(var actor in UnitController.ActiveUnits)
            {
                var candidate=actor!=null?actor.GetComponent<HealthComponent>():null;
                if(!Valid(candidate) || Vector2.Distance(candidate.transform.position,home)>data.detectionRadius) continue;
                float distance=((Vector2)(candidate.transform.position-transform.position)).sqrMagnitude;
                if(distance<closest) { best=candidate; closest=distance; }
            }
            return best;
        }

        private void Detonate()
        {
            state=State.Hidden;
            Vector3 position=transform.position;
            var targets=new List<UnitController>(UnitController.ActiveUnits);
            foreach(var actor in targets)
            {
                if(actor==null || actor.Faction is not (Faction.Blue or Faction.Red)
                    || Vector2.Distance(actor.transform.position,position)>data.explosionRadius) continue;
                var receiver=actor.GetComponent<HealthComponent>();
                if(receiver!=null && receiver.ReceiveAttack(health.CombatData.BaseDamage,
                    UnitType.GoblinBarrel,AttackDelivery.Projectile,1f,false,unit))
                    actor.GetComponent<UnitMovement>()?.ApplyDeterministicKnockback(position,data.knockback);
            }
            BuildingPresentationCatalog effects=BuildingPresentationCatalog.Load();
            BarrelExplosionVfx.Spawn(position,effects,data.explosionScale);
            health.TakeDamage(health.CurrentHP);
        }
    }

    public sealed class BarrelExplosionVfx : MonoBehaviour
    {
        private Vector3 position;
        private float scale;
        private float remaining=0.06f;
        private BuildingPresentationCatalog effects;
        public static void Spawn(Vector3 p,BuildingPresentationCatalog catalog,float size)
        {
            if(catalog==null) return;
            OneShotSpriteVfx.Spawn("BarrelExplosion01",p,catalog.Explosion01,10f,size,19,Faction.Goblin);
            var root=new GameObject("BarrelExplosionDelay");
            var delay=root.AddComponent<BarrelExplosionVfx>();
            delay.position=p; delay.effects=catalog; delay.scale=size;
        }
        private void Update()
        {
            remaining-=Time.deltaTime;
            if(remaining>0) return;
            OneShotSpriteVfx.Spawn("BarrelExplosion02",position,effects.Explosion02,10f,scale,19,Faction.Goblin);
            Destroy(gameObject);
        }
    }
}
