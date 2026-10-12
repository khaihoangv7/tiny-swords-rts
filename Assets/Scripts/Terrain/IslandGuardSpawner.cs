using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    // This component is attached only to map three. Each prefab owns its gameplay data.
    public sealed class IslandGuardSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] prefabs = System.Array.Empty<GameObject>();
        [SerializeField] private Vector2[] positions = System.Array.Empty<Vector2>();
        [SerializeField] private GoblinHutData hutData;
        [SerializeField] private Vector2Int[] huts = System.Array.Empty<Vector2Int>();

        private void Start()
        {
            GridManager grid=FindAnyObjectByType<GridManager>();
            if(hutData!=null && grid!=null)
                for(int i=0;i<huts.Length;i++)
                {
                    var hut=new GameObject("GoblinHut_"+i); hut.transform.SetParent(transform,false);
                    hut.AddComponent<GoblinHut>().Configure(hutData,huts[i],grid);
                }
            for (int i=0;i<prefabs.Length && i<positions.Length;i++)
                if (prefabs[i]!=null)
                    Instantiate(prefabs[i],positions[i],Quaternion.identity,transform);
        }
    }
}
