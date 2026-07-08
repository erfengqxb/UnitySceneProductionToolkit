using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SceneProductionToolkit.Runtime
{
    /// <summary>
    /// NPC 控制器 — 状态机驱动角色动画与行为
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcController : MonoBehaviour
    {
        public enum NpcState
        {
            Idle,
            Walking,
            Running,
            LookingAtCamera,
            Talking,
            Patrolling,
            Custom
        }

        [Header("基本设置")]
        [SerializeField] private string npcId = "NPC_默认";
        [SerializeField] private NpcState initialState = NpcState.Idle;

        [Header("动画参数")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string stateParameter = "State";
        [SerializeField] private string idleTrigger = "Idle";
        [SerializeField] private string walkTrigger = "Walk";
        [SerializeField] private string talkTrigger = "Talk";
        [SerializeField] private string waveTrigger = "Wave";

        [Header("看向目标")]
        [SerializeField] private bool enableLookAt = true;
        [SerializeField] private Transform lookAtTarget;
        [SerializeField] [Range(0f, 100f)] private float lookAtWeight = 0.8f;
        [SerializeField] [Range(0f, 180f)] private float maxLookAngle = 80f;

        [Header("移动参数")]
        [SerializeField] private float walkSpeed = 1.2f;
        [SerializeField] private float runSpeed = 3.5f;
        [SerializeField] private float rotationSpeed = 360f;
        [SerializeField] private float stoppingDistance = 0.1f;

        [Header("巡逻")]
        [SerializeField] private bool enablePatrol;
        [SerializeField] private List<Transform> patrolPoints;
        [SerializeField] private float patrolWaitTime = 2f;
        [SerializeField] private bool loopPatrol = true;

        private Animator animator;
        private NavMeshAgent navAgent;
        private NpcState currentState;
        private NpcState previousState;
        private float stateEnterTime;

        private int currentPatrolIndex;
        private float patrolWaitTimer;
        private bool waitingAtPoint;

        private bool isLookingAtCamera;
        private Transform cachedMainCamera;

        private Vector3 targetPosition;
        private bool hasMoveTarget;

        public string NpcId => npcId;
        public NpcState CurrentState => currentState;
        public bool IsMoving => currentState == NpcState.Walking || currentState == NpcState.Running;
        public Animator Animator => animator;
        public bool EnableLookAt { get => enableLookAt; set => enableLookAt = value; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.speed = walkSpeed;
                navAgent.stoppingDistance = stoppingDistance;
            }
            if (Camera.main != null)
                cachedMainCamera = Camera.main.transform;
        }

        private void Start() { SetState(initialState); }

        private void Update()
        {
            UpdateState();
            UpdateAnimatorParameters();
            UpdateLookAt();
            UpdatePatrol();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableLookAt || !Application.isPlaying) return;

            Transform target = isLookingAtCamera ? cachedMainCamera : lookAtTarget;
            if (target == null) return;

            float angle = Vector3.Angle(transform.forward, (target.position - transform.position).normalized);
            if (angle <= maxLookAngle)
            {
                animator.SetLookAtWeight(lookAtWeight, 0.3f, 0.7f, 0.5f, 0.5f);
                animator.SetLookAtPosition(target.position + Vector3.up * 1.5f);
            }
            else
            {
                animator.SetLookAtWeight(0f);
            }
        }

        public void SetState(NpcState newState)
        {
            if (currentState == newState) return;
            previousState = currentState;
            currentState = newState;
            stateEnterTime = Time.time;

            switch (newState)
            {
                case NpcState.Idle: animator.SetTrigger(idleTrigger); StopMovement(); break;
                case NpcState.Walking: animator.SetTrigger(walkTrigger); if (navAgent != null) navAgent.speed = walkSpeed; break;
                case NpcState.Running: animator.SetTrigger(walkTrigger); if (navAgent != null) navAgent.speed = runSpeed; break;
                case NpcState.LookingAtCamera: isLookingAtCamera = true; animator.SetTrigger(idleTrigger); break;
                case NpcState.Talking: animator.SetTrigger(talkTrigger); StopMovement(); break;
            }

            Debug.Log($"[NPC:{npcId}] 状态: {previousState} → {newState}");
        }

        public void MoveTo(Vector3 destination)
        {
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(destination);
                SetState(NpcState.Walking);
            }
            else
            {
                targetPosition = destination;
                hasMoveTarget = true;
                SetState(NpcState.Walking);
            }
        }

        public void LookAt(Transform target) { lookAtTarget = target; isLookingAtCamera = false; }
        public void LookAtCamera() { isLookingAtCamera = true; lookAtTarget = null; }
        public void StopLooking() { isLookingAtCamera = false; lookAtTarget = null; }
        public void Wave() { animator.SetTrigger(waveTrigger); }
        public void Talk() { SetState(NpcState.Talking); }

        public void StopMovement()
        {
            if (navAgent != null && navAgent.isOnNavMesh) navAgent.ResetPath();
            hasMoveTarget = false;
        }

        public void SetPatrolPoints(List<Transform> points)
        {
            patrolPoints = points;
            enablePatrol = points != null && points.Count > 0;
            currentPatrolIndex = 0;
        }

        public void PlayCustomAnimation(string triggerName)
        {
            animator.SetTrigger(triggerName);
            SetState(NpcState.Custom);
        }

        public float GetStateDuration() => Time.time - stateEnterTime;

        private void UpdateState()
        {
            switch (currentState)
            {
                case NpcState.Walking:
                case NpcState.Running:
                    bool reached = false;
                    if (navAgent != null && navAgent.isOnNavMesh)
                        reached = !navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance;
                    else if (hasMoveTarget)
                        reached = Vector3.Distance(transform.position, targetPosition) <= stoppingDistance;

                    if (reached) SetState(NpcState.Idle);
                    break;

                case NpcState.Talking:
                    if (GetStateDuration() > 3f) SetState(NpcState.Idle);
                    break;
            }
        }

        private void UpdateAnimatorParameters()
        {
            float speed = 0f;
            if (navAgent != null && navAgent.isOnNavMesh)
                speed = navAgent.velocity.magnitude;
            animator.SetFloat(speedParameter, speed);
            animator.SetInteger(stateParameter, (int)currentState);
        }

        private void UpdateLookAt()
        {
            if (isLookingAtCamera && cachedMainCamera != null)
                lookAtTarget = cachedMainCamera;
        }

        private void UpdatePatrol()
        {
            if (!enablePatrol || patrolPoints == null || patrolPoints.Count == 0) return;

            if (currentState == NpcState.Idle)
            {
                if (waitingAtPoint)
                {
                    patrolWaitTimer += Time.deltaTime;
                    if (patrolWaitTimer >= patrolWaitTime)
                    {
                        waitingAtPoint = false;
                        MoveToNextPatrolPoint();
                    }
                }
                else
                {
                    MoveToNextPatrolPoint();
                }
            }
        }

        private void MoveToNextPatrolPoint()
        {
            if (patrolPoints.Count == 0) return;
            var target = patrolPoints[currentPatrolIndex];
            if (target != null) MoveTo(target.position);

            currentPatrolIndex++;
            if (currentPatrolIndex >= patrolPoints.Count)
            {
                if (loopPatrol) currentPatrolIndex = 0;
                else enablePatrol = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (patrolPoints != null)
                foreach (var pt in patrolPoints)
                    if (pt != null) Gizmos.DrawWireSphere(pt.position, 0.3f);
        }
    }
}
