using UnityEngine;

public class AIWaypointPath : MonoBehaviour
{
    public Transform[] waypoints;

    // Returns the waypoint at the given index
    public Transform GetWaypoint(int index)
    {
        if (waypoints == null || waypoints.Length == 0) return null;
        return waypoints[index % waypoints.Length];
    }

    public int WaypointCount => waypoints != null ? waypoints.Length : 0;

    // Draw the path in the editor so you can see it
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 1f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(waypoints[i].position, next.position);
        }
    }
}
