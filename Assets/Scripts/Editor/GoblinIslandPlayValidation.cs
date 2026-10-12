using System;
using System.Linq;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class GoblinIslandPlayValidation
    {
        private const string Pending="GoblinIsland.PlayValidation";
        private static double started,readySince;
        private static int stage;
        private static float deadline;
        private static UnitMovement probe;
        private static HealthComponent blastBlue,blastRed;
        private static HealthComponent immuneGoblin,attackProbe,cancelProbe;
        private static UnitController torch,tnt;
        private static GoldMineObjective mine;
        private static float mineHP;
        private static GoblinBarrelCombat barrel;
        private static AmbientCloudDrift[] clouds;
        private static Vector3[] cloudStarts;
        private static readonly Vector2Int[] Waypoints={new(35,18),new(35,33),new(35,26),new(22,21),new(16,21),
            new(4,8),new(10,13),new(11,24),new(5,28),new(10,38),new(53,38),new(51,7)};
        static GoblinIslandPlayValidation() => EditorApplication.update+=Tick;

        public static void RunBatch()
        {
            started=EditorApplication.timeSinceStartup; stage=0; readySince=0;
            SessionState.SetBool(Pending,true);
        }

        private static void Require(bool valid,string reason)
        { if(!valid) throw new InvalidOperationException(reason); }

        private static void Tick()
        {
            if(!SessionState.GetBool(Pending,false)) return;
            if(started==0) started=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-started>200) { Finish(new TimeoutException("Map 3 validation timed out.")); return; }
            if(!EditorApplication.isPlaying)
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating) { readySince=0; return; }
                if(readySince==0) readySince=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-readySince<3) return;
                EditorSceneManager.OpenScene(GoblinIslandMapBuilder.ScenePath);
                EditorApplication.isPlaying=true; return;
            }
            if(Time.time<0.6f) return;
            try
            {
                if(stage==0) SetupAndValidate();
                else if(stage<=Waypoints.Length)
                {
                    if(Time.time>deadline) throw new TimeoutException("Live Pawn stalled en route to "+Waypoints[stage-1]);
                    if(probe.IsMoving) return;
                    GridManager grid=probe.GridManager; Vector2Int p=grid.WorldToGrid(probe.transform.position);
                    Require(p==Waypoints[stage-1],"Pawn finished in the wrong cell: "+p);
                    Debug.Log("GOBLIN_ISLAND_LIVE_ROUTE_PASSED: "+p);
                    stage++;
                    if(stage<=Waypoints.Length) IssueWaypoint();
                    else BeginBarrelTest();
                }
                else if(stage==Waypoints.Length+1)
                {
                    if(Time.time>deadline) throw new TimeoutException("Barrel did not detonate.");
                    if(barrel.GetComponent<UnitController>().IsAlive) return;
                    Require(blastBlue.CurrentHP==blastBlue.MaxHP-60 && blastRed.CurrentHP==blastRed.MaxHP-60,
                        "Barrel must damage Blue/Red once for its configured blast value.");
                    Require(immuneGoblin.CurrentHP==immuneGoblin.MaxHP && mine.CurrentHP==mineHP,
                        "The blast must not damage a nearby Goblin or the mine.");
                    var effects=UnityEngine.Object.FindObjectsByType<OneShotSpriteVfx>(FindObjectsSortMode.None);
                    Require(effects.Any(v=>v.name=="BarrelExplosion01"&&Mathf.Approximately(v.transform.localScale.x,0.8f)),
                        "Small building explosion sheet is missing.");
                    Debug.Log("GOBLIN_BARREL_BLAST_PASSED: one hit per Blue/Red target, no mine/goblin damage, small building VFX.");
                    Require(clouds.Where((c,i)=>Vector3.Distance(c.transform.position,cloudStarts[i])>0.01f).Count()==12,
                        "Every large/small cloud must drift.");
                    Debug.Log("GOBLIN_ISLAND_CLOUD_DRIFT_PASSED: all 12 clouds moved.");
                    stage++; BeginTorchTest();
                }
                else if(stage==Waypoints.Length+2)
                {
                    if(Time.time>deadline) throw new TimeoutException("Torch did not attack.");
                    if(attackProbe.CurrentHP==attackProbe.MaxHP) return;
                    Require(attackProbe.CurrentHP==attackProbe.MaxHP-28,"Torch damage differs from its data.");
                    Require(torch.GetComponent<UnitAnimator>().VisualRenderer.sprite.name.StartsWith("Torch_Yellow_"),"Torch attack sprite is missing.");
                    torch.GetComponent<CombatController>().enabled=false;
                    UnityEngine.Object.Destroy(attackProbe.gameObject);
                    Debug.Log("GOBLIN_TORCH_ATTACK_PASSED: normal combat damage and mapped attack presentation.");
                    stage++; BeginTntTest();
                }
                else if(stage==Waypoints.Length+3)
                {
                    if(Time.time>deadline) throw new TimeoutException("TNT did not release and hit with its dynamite.");
                    if(attackProbe.CurrentHP==attackProbe.MaxHP) return;
                    Require(attackProbe.CurrentHP==attackProbe.MaxHP-18,"TNT must apply one configured explosion hit.");
                    Require(Vector2.Distance(tnt.GetComponent<UnitAnimator>().VisualRenderer.transform.position,tnt.transform.position)<0.1f,
                        "TNT throw row changed the ground anchor.");
                    tnt.GetComponent<GoblinTntCombat>().enabled=false;
                    UnityEngine.Object.Destroy(attackProbe.gameObject);
                    Debug.Log("GOBLIN_TNT_THROW_PASSED: gameplay release, separate dynamite, explosion hit and stable multi-row anchor.");
                    stage++; BeginCancelTest(GoblinIslandLayout.Shift(new(35.5f,21.5f)));
                }
                else if(stage==Waypoints.Length+4 || stage==Waypoints.Length+6)
                {
                    if(Time.time>deadline) throw new TimeoutException("Cancellation probe did not arm.");
                    if(!barrel.IsArmed) return;
                    if(stage==Waypoints.Length+4)
                        barrel.GetComponent<HealthComponent>().TakeDamage(10000);
                    else
                    {
                        ValidateHuts();
                        MatchManager.Instance.RedMainCastle.ReceiveBuildingDamage(10000);
                    }
                    stage++; deadline=Time.time+1.2f;
                }
                else if(stage==Waypoints.Length+5 || stage==Waypoints.Length+7)
                {
                    if(Time.time<deadline) return;
                    Require(cancelProbe.CurrentHP==cancelProbe.MaxHP,"A cancelled Barrel fuse still dealt damage.");
                    Require(!barrel.IsArmed,"A cancelled Barrel still reports armed state.");
                    if(stage==Waypoints.Length+5)
                    {
                        Debug.Log("GOBLIN_BARREL_DEATH_CANCEL_PASSED: killing an armed Barrel cancels its blast.");
                        UnityEngine.Object.Destroy(cancelProbe.gameObject);
                        stage++; BeginCancelTest(GoblinIslandLayout.Shift(new(37.5f,18.5f)));
                    }
                    else
                    {
                        Require(MatchManager.Instance.State==MatchState.Ended,"Main Castle death did not end the match.");
                        Debug.Log("GOBLIN_BARREL_MATCH_END_CANCEL_PASSED: ended match cancels the armed fuse.");
                        Finish(null);
                    }
                }
            }
            catch(Exception error) { Finish(error); }
        }

        private static void SetupAndValidate()
        {
            Require(SceneManager.GetActiveScene().path==GoblinIslandMapBuilder.ScenePath,"Wrong gameplay scene.");
            var units=UnitController.ActiveUnits;
            Require(units.Count(u=>u.Faction==Faction.Blue)==3 && units.Count(u=>u.Faction==Faction.Red)==3
                && units.Count(u=>u.Faction==Faction.Goblin)==12,"Expected three Pawns per faction and twelve neutral guards.");
            Require(UnityEngine.Object.FindObjectsByType<CastleDropOff>(FindObjectsSortMode.None).Length==2,"Expected two Main Castles.");
            Require(MatchManager.Instance!=null && MatchManager.AllowsGameplay,"Match did not begin.");
            mine=UnityEngine.Object.FindAnyObjectByType<GoldMineObjective>();
            Require(mine!=null && Vector2.Distance(mine.transform.position,(Vector2)GoblinIslandLayout.MineOrigin+Vector2.one)<0.01f,"Mine is not on its authored island footprint.");
            var grid=UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(UnityEngine.Object.FindObjectsByType<GoblinHut>(FindObjectsSortMode.None).Length==3,"Expected three neutral Goblin huts.");
            Require(grid.Width==66 && grid.GetCell(53,9).HeightLevel==0 && grid.GetCell(53,40).HeightLevel==0,
                "The revised Castles must stand on flat ground.");
            Require(grid.GetCell(25,18).HeightLevel==0 && grid.GetCell(16,21).HeightLevel==0,
                "The island and bridge approaches must stay on flat ground.");
            for(int i=0;i<GoblinIslandLayout.HillClearings.Length;i++)
            {
                var p=GoblinIslandLayout.HillClearings[i];
                Require(grid.GetCell(p.x,p.y).HeightLevel==1+i%2,"A western hill tier was not baked.");
            }
            Require(units.Where(u=>u.Faction is Faction.Blue or Faction.Red).All(u=>u.transform.position.x>=18)
                && units.Count(u=>u.Faction==Faction.Goblin && u.transform.position.x<18 && u.transform.position.y>=33)==0
                && units.Count(u=>u.Faction==Faction.Goblin && u.transform.position.x<18
                    && u.transform.position.y>=20 && u.transform.position.y<31)==3,
                "The northern hill must have no guards and the middle hill must retain three.");
            var layout=AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(GoblinIslandMapBuilder.LayoutPath);
            Require(ResourceNode.ActiveNodes.Count==layout.resources.Length,"Live resource count differs from the authored layout.");
            foreach(var commander in UnityEngine.Object.FindObjectsByType<RuleBasedCommander>(FindObjectsSortMode.None)) commander.enabled=false;
            foreach(var u in units.Where(u=>u.Faction==Faction.Red)) u.GetComponent<PawnGatherer>()?.CancelJob();
            foreach(var guard in units.Where(u=>u.Faction==Faction.Goblin))
            {
                var animation=guard.GetComponent<UnitAnimator>();
                Require(animation.VisualRenderer!=null && Vector2.Distance(animation.VisualRenderer.transform.position,guard.transform.position)<0.1f,
                    "Multi-row goblin animation has a displaced anchor.");
                Require(Mathf.Approximately(animation.VisualRenderer.transform.localScale.x,GoblinIslandLayout.GuardVisualScale)
                    && guard.transform.localScale==Vector3.one,"Only the Goblin visual child may be enlarged.");
                var set=animation.AnimationSet;
                if(guard.GetComponent<HealthComponent>().CombatData.UnitType==UnitType.GoblinTorch)
                { Require(set.Idle.Length==7&&set.Run.Length==6&&set.AttackDown.Length==6&&set.AttackUp.Length==6,"Torch frame rows are incomplete.");
                    torch=guard; guard.GetComponent<CombatController>().enabled=false; }
                if(guard.GetComponent<GoblinTntCombat>()!=null)
                { Require(set.Idle.Length==6&&set.Run.Length==6&&set.Attack.Length==7,"TNT frame rows are incomplete.");
                    tnt=guard; guard.GetComponent<GoblinTntCombat>().enabled=false; }
            }
            foreach(var b in UnityEngine.Object.FindObjectsByType<GoblinBarrelCombat>(FindObjectsSortMode.None)) b.enabled=false;
            barrel=UnityEngine.Object.FindObjectsByType<GoblinBarrelCombat>(FindObjectsSortMode.None)
                .OrderBy(b=>Vector2.Distance(b.transform.position,GoblinIslandLayout.Shift(new(38.5f,21.5f)))).First();
            clouds=UnityEngine.Object.FindObjectsByType<AmbientCloudDrift>(FindObjectsSortMode.None);
            cloudStarts=clouds.Select(c=>c.transform.position).ToArray(); Require(clouds.Length==12,"Cloud composition changed.");
            probe=units.First(u=>u.Faction==Faction.Blue).GetComponent<UnitMovement>();
            Time.timeScale=4f;
            stage=1; IssueWaypoint();
            Debug.Log("GOBLIN_ISLAND_INITIAL_STATE_PASSED: playable match, correct mine/resources/guard animation rows.");
        }

        private static void IssueWaypoint()
        {
            Require(probe.TryIssueMove(Waypoints[stage-1]),"Could not issue live movement toward "+Waypoints[stage-1]);
            deadline=Time.time+75;
        }

        private static HealthComponent Target(string name,Faction faction,Vector3 position)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/BlueWarriorTest.prefab");
            var obj=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity); obj.name=name;
            obj.GetComponent<UnitController>().Configure(faction); obj.GetComponent<CombatController>().enabled=false;
            var movement=new SerializedObject(obj.GetComponent<UnitMovement>());
            movement.FindProperty("separationSpeed").floatValue=0f;
            movement.ApplyModifiedPropertiesWithoutUndo();
            return obj.GetComponent<HealthComponent>();
        }
        private static void BeginBarrelTest()
        {
            // The blast center remains on valid ground beside the real mine.
            // Two playable targets and a neutral guard lie inside the same blast.
            Vector3 p=GoblinIslandLayout.Shift(new(38.5f,21.5f));
            barrel.transform.position=p;
            blastBlue=Target("Barrel_Blue_Probe",Faction.Blue,p+Vector3.right*2f);
            blastRed=Target("Barrel_Red_Probe",Faction.Red,p+Vector3.right*2f+Vector3.up*0.7f);
            var innocent=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Units/IslandGoblinTNT.prefab"),p+new Vector3(1.5f,0.5f),Quaternion.identity);
            innocent.GetComponent<GoblinTntCombat>().enabled=false;
            immuneGoblin=innocent.GetComponent<HealthComponent>(); mineHP=mine.CurrentHP;
            barrel.enabled=true; deadline=Time.time+8; Time.timeScale=1f;
        }

        private static void BeginTorchTest()
        {
            attackProbe=Target("Torch_Attack_Probe",Faction.Blue,torch.transform.position+Vector3.right*0.8f);
            torch.GetComponent<CombatController>().enabled=true; deadline=Time.time+4;
        }
        private static void BeginTntTest()
        {
            attackProbe=Target("TNT_Throw_Probe",Faction.Blue,tnt.transform.position+Vector3.down*2f);
            tnt.GetComponent<GoblinTntCombat>().enabled=true; deadline=Time.time+5;
        }
        private static void BeginCancelTest(Vector3 p)
        {
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Units/GoblinBarrel.prefab"),p,Quaternion.identity);
            barrel=obj.GetComponent<GoblinBarrelCombat>();
            cancelProbe=Target("Barrel_Cancel_Probe",Faction.Blue,p+Vector3.right*0.8f);
            deadline=Time.time+5;
        }
        private static void ValidateHuts()
        {
            var grid=UnityEngine.Object.FindAnyObjectByType<GridManager>();
            var data=AssetDatabase.LoadAssetAtPath<GoblinHutData>("Assets/Data/GoblinHutData.asset");
            foreach(var hut in UnityEngine.Object.FindObjectsByType<GoblinHut>(FindObjectsSortMode.None))
            {
                Vector2Int p=hut.FootprintOrigin;
                Require(hut.IsAlive && hut.MaxHP==400 && !hut.IsProductionReady && grid.GetCell(p.x,p.y).Occupied,
                    "Hut HP, production exclusion or footprint is wrong.");
                Require(hut.GetComponent<BuildingCombatTarget>().ReceiveAttack(400,UnitType.Warrior,AttackDelivery.Melee),
                    "Normal building combat cannot damage a Goblin hut.");
                Require(!hut.IsAlive && !grid.GetCell(p.x,p.y).Occupied && hut.PresentationRenderer.sprite==data.wreck,
                    "Hut destruction must release occupancy and use the verified wreck.");
                Require(MatchManager.Instance.State==MatchState.Playing,"Goblin hut destruction ended the match.");
            }
            Debug.Log("GOBLIN_HUT_DESTRUCTION_PASSED: all three huts use building damage, release footprints and leave native wrecks without ending the match.");
        }
        private static void Finish(Exception error)
        {
            SessionState.SetBool(Pending,false); Time.timeScale=1f;
            if(error!=null) { Debug.LogException(error); EditorApplication.Exit(1); }
            else { Debug.Log("GOBLIN_ISLAND_PLAY_VALIDATION_PASSED"); EditorApplication.Exit(0); }
        }
    }
}
