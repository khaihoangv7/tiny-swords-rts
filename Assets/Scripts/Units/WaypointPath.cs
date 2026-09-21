using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class WaypointPath
    {
        private readonly List<Vector3> waypoints = new();
        private int nextWaypoint;

        public IReadOnlyList<Vector3> Waypoints => waypoints;
        public int NextWaypointIndex => nextWaypoint;
        public bool HasRemainingWaypoints => nextWaypoint < waypoints.Count;

        public void Set(IReadOnlyList<Vector3> newWaypoints, Vector3 currentPosition, float arrivalTolerance)
        {
            waypoints.Clear();
            nextWaypoint = 0;
            if (newWaypoints == null)
                return;

            waypoints.AddRange(newWaypoints);
            while (nextWaypoint < waypoints.Count
                   && Vector3.Distance(currentPosition, waypoints[nextWaypoint]) <= arrivalTolerance)
                nextWaypoint++;
        }

        public void Clear()
        {
            waypoints.Clear();
            nextWaypoint = 0;
        }

        public void RestoreNextWaypoint(int index)
        {
            nextWaypoint = Mathf.Clamp(index, 0, waypoints.Count);
        }

        public Vector3 Advance(Vector3 currentPosition, float speed, float deltaTime, float arrivalTolerance,
            out bool completed)
        {
            float remainingDistance = Mathf.Max(0f, speed * deltaTime);
            Vector3 position = currentPosition;

            while (nextWaypoint < waypoints.Count)
            {
                Vector3 target = waypoints[nextWaypoint];
                float distance = Vector3.Distance(position, target);
                if (distance <= arrivalTolerance)
                {
                    position = target;
                    nextWaypoint++;
                    continue;
                }

                if (remainingDistance <= 0f)
                    break;

                if (distance <= remainingDistance)
                {
                    position = target;
                    remainingDistance -= distance;
                    nextWaypoint++;
                }
                else
                {
                    position = Vector3.MoveTowards(position, target, remainingDistance);
                    remainingDistance = 0f;
                }
            }

            completed = nextWaypoint >= waypoints.Count;
            return position;
        }
    }
}
