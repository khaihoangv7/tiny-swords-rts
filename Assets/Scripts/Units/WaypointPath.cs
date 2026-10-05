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
        public Vector3 CurrentWaypoint => HasRemainingWaypoints
            ? waypoints[nextWaypoint] : default;

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

        // Connector traversal deliberately consumes one endpoint at a time. This
        // prevents a large frame step from crossing the shared stair corner and
        // immediately continuing into a third cell before navigation can validate it.
        public Vector3 AdvanceCurrentWaypoint(Vector3 currentPosition, float speed,
            float deltaTime, float arrivalTolerance, out bool completed)
        {
            if (!HasRemainingWaypoints)
            {
                completed = true;
                return currentPosition;
            }

            Vector3 target = waypoints[nextWaypoint];
            float distance = Vector3.Distance(currentPosition, target);
            if (distance <= arrivalTolerance
                || distance <= Mathf.Max(0f, speed * deltaTime))
            {
                nextWaypoint++;
                completed = !HasRemainingWaypoints;
                return target;
            }

            completed = false;
            return Vector3.MoveTowards(currentPosition, target,
                Mathf.Max(0f, speed * deltaTime));
        }
    }
}
