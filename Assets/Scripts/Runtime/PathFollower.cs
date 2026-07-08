using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SceneProductionToolkit.Runtime
{
    /// <summary>
    /// 路径跟随组件 — NavMesh / Transform 双模式
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        public enum MovementMode { NavMesh, DirectTransform }
        public enum PathCompleteBehavior { Stop, Loop, PingPong, DestroyOnComplete }

        [Header("移动模式")]
        [SerializeField] private MovementMode movementMode = MovementMode.NavMesh;
        [SerializeField] private PathCompleteBehavior completeBehavior = PathCompleteBehavior.Loop;

        [Header("参数")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeed = 360f;
        [SerializeField] private float waypointThreshold = 0.5f;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool faceMovementDirection = true;

        [Header("路径")]
        [SerializeField] private List<Vector3> waypoints = new List<Vector3>();
        [SerializeField] private List<Transform> waypointTransforms = new List<Transform>();

        private NavMeshAgent navAgent;
        private int currentIndex;
        private bool isMoving;
        private bool movingForward = true;
        private Vector3 targetPosition;

        public bool IsMoving => isMoving;
        public int CurrentWaypointIndex => currentIndex;
        public int TotalWaypoints => Mathf.Max(waypoints.Count, waypointTransforms.Count);
        public float Progress => TotalWaypoints > 0 ? (float)currentIndex / TotalWaypoints : 0f;

        public event System.Action<int> OnWaypointReached;
        public event System.Action OnPathCompleted;
        public event System.Action OnPathStarted;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            SyncWaypointsFromTransforms();
        }

        private void Start()
        {
            if (autoStart && TotalWaypoints > 0) StartFollowing();
        }

        private void Update()
        {
            if (!isMoving) return;
            if (movementMode == MovementMode.NavMesh) UpdateNavMeshMovement();
            else UpdateDirectMovement();
        }

        public void StartFollowing()
        {
            if (TotalWaypoints == 0)
            {
                Debug.LogWarning("[路径跟随] 没有路径点");
                return;
            }
            SyncWaypointsFromTransforms();
            isMoving = true;
            currentIndex = 0;
            movingForward = true;
            SetDestination(GetWaypoint(0));
            OnPathStarted?.Invoke();
            Debug.Log($"[路径跟随] 开始: {TotalWaypoints} 个路径点");
        }

        public void StopFollowing()
        {
            isMoving = false;
            if (navAgent != null && navAgent.isOnNavMesh) navAgent.ResetPath();
        }

        public void TogglePause()
        {
            isMoving = !isMoving;
            if (isMoving) SetDestination(GetWaypoint(currentIndex));
            else if (navAgent != null && navAgent.isOnNavMesh) navAgent.isStopped = true;
        }

        public void JumpToWaypoint(int index)
        {
            if (index < 0 || index >= TotalWaypoints) return;
            currentIndex = index;
            transform.position = GetWaypoint(index);
            SetDestination(GetNextWaypoint());
        }

        public void SetWaypoints(List<Vector3> points) { waypoints = points; waypointTransforms.Clear(); SyncWaypointsFromTransforms(); }
        public void SetWaypointTransforms(List<Transform> points) { waypointTransforms = points; waypoints.Clear(); SyncWaypointsFromTransforms(); }
        public void AddWaypoint(Vector3 point) { waypoints.Add(point); }
        public void AddWaypoint(Transform pointTransform) { waypointTransforms.Add(pointTransform); waypoints.Add(pointTransform.position); }

        public void SetSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0.01f, speed);
            if (navAgent != null) navAgent.speed = moveSpeed;
        }

        public Vector3 GetCurrentTarget() => GetWaypoint(currentIndex);

        private void SyncWaypointsFromTransforms()
        {
            if (waypointTransforms.Count > 0)
            {
                waypoints.Clear();
                foreach (var t in waypointTransforms)
                    if (t != null) waypoints.Add(t.position);
            }
        }

        private Vector3 GetWaypoint(int index)
        {
            if (index >= 0 && index < waypoints.Count) return waypoints[index];
            if (index >= 0 && index < waypointTransforms.Count && waypointTransforms[index] != null)
                return waypointTransforms[index].position;
            return transform.position;
        }

        private Vector3 GetNextWaypoint()
        {
            int next = GetNextIndex();
            return next >= 0 ? GetWaypoint(next) : transform.position;
        }

        private int GetNextIndex()
        {
            if (TotalWaypoints <= 1) return -1;
            if (movingForward) return currentIndex + 1 < TotalWaypoints ? currentIndex + 1 : -1;
            return currentIndex - 1 >= 0 ? currentIndex - 1 : -1;
        }

        private void SetDestination(Vector3 destination)
        {
            targetPosition = destination;
            if (movementMode == MovementMode.NavMesh && navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(destination);
                navAgent.speed = moveSpeed;
                navAgent.isStopped = false;
            }
        }

        private void UpdateNavMeshMovement()
        {
            if (navAgent == null || !navAgent.isOnNavMesh)
            {
                UpdateDirectMovement();
                return;
            }
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + waypointThreshold)
            {
                OnWaypointReached?.Invoke(currentIndex);
                AdvanceToNext();
            }
        }

        private void UpdateDirectMovement()
        {
            float dist = Vector3.Distance(transform.position, targetPosition);
            if (dist <= waypointThreshold)
            {
                OnWaypointReached?.Invoke(currentIndex);
                AdvanceToNext();
                return;
            }

            Vector3 dir = (targetPosition - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;

            if (faceMovementDirection)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            }
        }

        private void AdvanceToNext()
        {
            int next = GetNextIndex();
            if (next >= 0)
            {
                currentIndex = movingForward ? currentIndex + 1 : currentIndex - 1;
                SetDestination(GetWaypoint(currentIndex));
            }
            else
            {
                HandlePathComplete();
            }
        }

        private void HandlePathComplete()
        {
            OnPathCompleted?.Invoke();
            switch (completeBehavior)
            {
                case PathCompleteBehavior.Stop: isMoving = false; break;
                case PathCompleteBehavior.Loop: currentIndex = 0; SetDestination(GetWaypoint(0)); break;
                case PathCompleteBehavior.PingPong: movingForward = !movingForward; currentIndex = movingForward ? 0 : TotalWaypoints - 1; SetDestination(GetWaypoint(currentIndex)); break;
                case PathCompleteBehavior.DestroyOnComplete: isMoving = false; Destroy(gameObject, 0.5f); break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            SyncWaypointsFromTransforms();
            if (waypoints.Count == 0) return;

            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawWireSphere(waypoints[i], 0.2f);
                if (i > 0) Gizmos.DrawLine(waypoints[i - 1], waypoints[i]);
            }
            if (completeBehavior == PathCompleteBehavior.Loop && waypoints.Count > 2)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(waypoints[0], waypoints[waypoints.Count - 1]);
            }
        }
    }
}
