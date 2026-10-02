using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using TinyKingdom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class RiverMountainsPlayValidation
    {
        private const string Pending = "RiverMountains.PlayValidation";
        private const string ScenePath = "Assets/Scenes/LinearLane.unity";
        private static double started;
        private static double readySince;
        private static double playStarted;
        private static UnitMovement stairProbe;
        private static int travelStage;
        private static int visitedHeights;
        private static AmbientCloudDrift[] clouds;
        private static Vector3[] cloudStartPositions;
        private static readonly Vector2Int LaneDestination=new(19,22);
        private static readonly Vector2Int CastleDestination=new(5,21);

        static RiverMountainsPlayValidation() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            started = EditorApplication.timeSinceStartup;
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 90)
            {
                Finish(new TimeoutException("Straight Lane Play Mode validation timed out."));
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    readySince = 0;
                    return;
                }
                if (readySince == 0) readySince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - readySince > 8
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Existing startup asset preparers open Map.unity. Select
                    // map two AFTER they settle, before entering the test match.
                    if (EditorSceneManager.GetActiveScene().path!=ScenePath)
                        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                    EditorApplication.EnterPlaymode();
                }
                return;
            }
            if (playStarted == 0) playStarted = EditorApplication.timeSinceStartup;
            // Validate the authored opening before the Commander can finish
            // its first four-second Pawn training request.
            if (EditorApplication.timeSinceStartup - playStarted < 1.5) return;
            try
            {
                if (travelStage==0)
                {
                    Validate();
                    stairProbe=UnityEngine.Object.FindObjectsByType<UnitController>()
                        .First(item=>item.Faction==Faction.Blue).GetComponent<UnitMovement>();
                    visitedHeights=1<<2;
                    if (!stairProbe.TryIssueMove(LaneDestination))
                        throw new InvalidOperationException("Pawn cannot begin the two-tier descent.");
                    travelStage=1;
                }
                else
                {
                    GridManager grid=stairProbe.GridManager;
                    Vector2Int position=grid.WorldToGrid(stairProbe.transform.position);
                    visitedHeights|=1<<grid.GetCell(position.x,position.y).HeightLevel;
                    Vector2Int destination=travelStage==1?LaneDestination:CastleDestination;
                    if (Vector2.Distance(stairProbe.transform.position,grid.GridToWorld(destination.x,destination.y))>0.3f) return;
                    if (travelStage==1)
                    {
                        if (visitedHeights!=7) throw new InvalidOperationException("Descent did not visit H2, H1 and H0.");
                        if (!stairProbe.TryIssueMove(CastleDestination))
                            throw new InvalidOperationException("Pawn cannot return up both stair gates.");
                        travelStage=2;
                    }
                    else
                    {
                        Debug.Log("LINEAR_LANE_STAIR_ROUND_TRIP_PASSED: live Pawn descended H2/H1/H0 and returned to Castle court.");
                        if (clouds.Any(cloud=>cloud==null) || clouds.Where((cloud,i)=>
                            Vector3.Distance(cloud.transform.position,cloudStartPositions[i])>0.01f).Count()!=10)
                            throw new InvalidOperationException("The ten ambient clouds did not move during Play Mode.");
                        Debug.Log("LINEAR_LANE_CLOUD_DRIFT_PASSED: all ten large/small clouds moved independently of navigation.");
                        Finish(null);
                    }
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Finish(Exception error)
        {
            SessionState.SetBool(Pending, false);
            if (error != null)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("RIVER_MOUNTAINS_PLAY_VALIDATION_PASSED");
                EditorApplication.Exit(0);
            }
        }

        private static void Validate()
        {
            if (SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("The test is not running the second map scene.");
            CastleDropOff[] castles = UnityEngine.Object.FindObjectsByType<CastleDropOff>();
            if (castles.Length != 2 || castles.Count(item => item.Faction == Faction.Blue) != 1
                || castles.Count(item => item.Faction == Faction.Red) != 1)
                throw new InvalidOperationException("Expected one living Castle per faction.");
            UnitController[] units = UnityEngine.Object.FindObjectsByType<UnitController>();
            int blueCount = units.Count(item => item.Faction == Faction.Blue);
            int redCount = units.Count(item => item.Faction == Faction.Red);
            if (blueCount != 3 || redCount != 3)
                throw new InvalidOperationException($"Expected three starting Pawns per faction; found Blue {blueCount}, Red {redCount}.");
            MatchManager match = UnityEngine.Object.FindAnyObjectByType<MatchManager>();
            if (match == null || !match.HasBothMainCastles || match.State != MatchState.Playing)
                throw new InvalidOperationException("The match did not start.");
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            if (grid == null || grid.NavigationData == null
                || grid.Width != RiverMountainsLayout.Width || grid.Height != RiverMountainsLayout.Height)
                throw new InvalidOperationException("The second map navigation is not active.");
            foreach (CastleDropOff castle in castles)
            {
                Vector2Int p=grid.WorldToGrid(castle.transform.position);
                if (grid.GetCell(p.x,p.y).HeightLevel!=2)
                    throw new InvalidOperationException("A main Castle is not on its upper court.");
            }
            if (UnityEngine.Object.FindObjectsByType<GoldMineObjective>().Length != 1)
                throw new InvalidOperationException("The central Gold Mine is missing or duplicated.");
            EconomyTestSetup setup = UnityEngine.Object.FindAnyObjectByType<EconomyTestSetup>();
            var serialized = new SerializedObject(setup);
            BattlefieldLayout layout = serialized.FindProperty("battlefieldLayout")
                .objectReferenceValue as BattlefieldLayout;
            if (layout == null || ResourceNode.ActiveNodes.Count != layout.resources.Length)
                throw new InvalidOperationException("Runtime resources differ from the second map layout.");
            if (ResourceNode.ActiveNodes.Count(node=>node.ResourceType==ResourceType.Food)!=40)
                throw new InvalidOperationException("Deleting the four pink-marked Sheep must leave 40 Food nodes.");
            foreach (ResourceNode sheep in ResourceNode.ActiveNodes.Where(node=>node.ResourceType==ResourceType.Food))
            {
                Vector2Int p=sheep.OccupiedCell;
                if (grid.GetCell(p.x,p.y).HeightLevel!=1)
                    throw new InvalidOperationException("A Sheep did not spawn on the expanded first tier: "+sheep.name);
            }
            Transform scenery=GameObject.Find("Map").transform.Find("BattlefieldScenery");
            Transform[] decorations=scenery.Cast<Transform>().ToArray();
            if (decorations.Count(item=>item.name.StartsWith("Lane_ExtraBush_"))!=4
                || decorations.Count(item=>item.name.StartsWith("Lane_Duck_"))!=6
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedBush_"))!=24
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedStone_"))!=6
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedWaterStone_"))!=4
                || scenery.GetComponentsInChildren<Collider2D>(true).Length!=0
                || scenery.GetComponentsInChildren<ResourceNode>(true).Length!=0)
                throw new InvalidOperationException("The new nonblocking bushes/rocks/Ducks did not survive into Play Mode.");
            clouds=scenery.GetComponentsInChildren<AmbientCloudDrift>();
            if (clouds.Length!=10 || !clouds.Any(cloud=>cloud.transform.localScale.x==1.1f)
                || !clouds.Any(cloud=>cloud.transform.localScale.x==1.8f))
                throw new InvalidOperationException("Expected ten clouds with both large and small native illustrations.");
            cloudStartPositions=clouds.Select(cloud=>cloud.transform.position).ToArray();
            Vector2Int blue = grid.WorldToGrid(units.First(item => item.Faction == Faction.Blue)
                .transform.position);
            Vector2Int red = grid.WorldToGrid(units.First(item => item.Faction == Faction.Red)
                .transform.position);
            if (AStarPathfinder.FindPath(grid, grid.GetCell(blue.x, blue.y),
                    grid.GetCell(28, 21)).Count == 0
                || AStarPathfinder.FindPath(grid, grid.GetCell(red.x, red.y),
                    grid.GetCell(31, 22)).Count == 0)
                throw new InvalidOperationException("A faction cannot reach the central Gold Mine.");
            RiverMountainsMapBuilder.ValidateLinearRoutes(grid, layout,
                grid.GetCell(blue.x, blue.y), grid.GetCell(red.x, red.y));
        }
    }

    [InitializeOnLoad]
    public static class MapSelectionPlayValidation
    {
        private const string Pending = "RiverMountains.MenuValidation";
        private static double started;
        private static double stageStarted;
        private static int stage;
        private static double readySince;

        static MapSelectionPlayValidation() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MapSelection.unity");
            started = EditorApplication.timeSinceStartup;
            stageStarted = started;
            stage = 0;
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 90)
            {
                Finish(new TimeoutException("Map selection validation timed out."));
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                { readySince=0; return; }
                if (readySince==0) readySince=EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup-readySince>8
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if (EditorSceneManager.GetActiveScene().path!="Assets/Scenes/MapSelection.unity")
                        EditorSceneManager.OpenScene("Assets/Scenes/MapSelection.unity",OpenSceneMode.Single);
                    EditorApplication.EnterPlaymode();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup - stageStarted < 1.5) return;
            try
            {
                if (stage == 0)
                {
                    if (UnityEngine.Object.FindAnyObjectByType<MapSelectionMenu>() == null
                        || UnityEngine.Object.FindAnyObjectByType<EventSystem>()?
                            .GetComponent<InputSystemUIInputModule>() == null)
                        throw new InvalidOperationException("The selection menu or its input module is missing.");
                    MapSelectionCard[] cards = UnityEngine.Object.FindObjectsByType<MapSelectionCard>();
                    if (cards.Length != 2)
                        throw new InvalidOperationException("Expected two selectable map cards.");
                    MapSelectionCard river = cards.FirstOrDefault(item => item.name == "STRAIGHT LANE");
                    if (river == null) throw new InvalidOperationException("Map 2 button is missing.");
                    stage = 1;
                    stageStarted = EditorApplication.timeSinceStartup;
                    var pointer = new PointerEventData(EventSystem.current)
                        { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute<IPointerClickHandler>(river.gameObject, pointer,
                        ExecuteEvents.pointerClickHandler);
                }
                else if (stage == 1)
                {
                    string active = SceneManager.GetActiveScene().name;
                    int castles = UnityEngine.Object.FindObjectsByType<CastleDropOff>().Length;
                    if (active != "LinearLane" || castles != 2)
                        throw new InvalidOperationException($"The map 2 button did not start a match (scene={active}, Castles={castles}).");
                    stage = 2;
                    stageStarted = EditorApplication.timeSinceStartup;
                    SceneManager.LoadScene("MapSelection");
                }
                else if (stage == 2)
                {
                    MapSelectionCard original = UnityEngine.Object.FindObjectsByType<MapSelectionCard>()
                        .FirstOrDefault(item => item.name == "TWO KINGDOMS");
                    if (original == null) throw new InvalidOperationException("Map 1 button is missing.");
                    stage = 3;
                    stageStarted = EditorApplication.timeSinceStartup;
                    var pointer = new PointerEventData(EventSystem.current)
                        { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute<IPointerClickHandler>(original.gameObject, pointer,
                        ExecuteEvents.pointerClickHandler);
                }
                else
                {
                    string active = SceneManager.GetActiveScene().name;
                    int castles = UnityEngine.Object.FindObjectsByType<CastleDropOff>().Length;
                    if (active != "Map" || castles != 2)
                        throw new InvalidOperationException($"The original map button did not start a match (scene={active}, Castles={castles}).");
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Finish(Exception error)
        {
            SessionState.SetBool(Pending, false);
            if (error != null)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("MAP_SELECTION_PLAY_VALIDATION_PASSED");
                EditorApplication.Exit(0);
            }
        }
    }
}
