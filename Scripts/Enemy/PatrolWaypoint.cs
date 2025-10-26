using UnityEngine;
using System.Collections;
using Opsive.UltimateCharacterController.Character;              
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Opsive.UltimateCharacterController.Demo.AI;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class PatrolWaypoint : MonoBehaviour
{
    [Header("Waypoints (in order)")]
    [SerializeField] private Transform[] waypoints;

    [Header("Behavior")]
    [SerializeField, Tooltip("Return to the first point after the last.")]
    private bool loop = true;

    [SerializeField, Tooltip("Seconds to wait after arriving at a point.")]
    private float waitAtPoint = 0.0f;

    [SerializeField, Tooltip("Distance considered 'arrived' at a waypoint.")]
    private float arriveDistance = 0.35f;

    [Header("Start")]
    [SerializeField, Tooltip("Begin patrol automatically on Enable (after UCC init).")]
    private bool startOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float gizmoPointRadius = 0.12f;
    [SerializeField] private Color pointColor = new Color(0.2f, 0.8f, 1f, 0.95f);
    [SerializeField] private Color lineColor = new Color(1f, 0.9f, 0.2f, 0.95f);

    private UltimateCharacterLocomotion _ucl;
    private PathfindingMovement _pathfind;
    private NavMeshAgentMovement _navMove; 
    private AgentMovement _agentMove;      
    private NavMeshAgent _nav;           

    private int _index;
    private bool _paused;
    private Coroutine _routine;
    private bool _ready;

    void Awake() {
        _ucl = GetComponent<UltimateCharacterLocomotion>();
    }

    void OnEnable()  { if (startOnEnable) _ = StartCoroutine(InitializeAndStart()); }
    void OnDisable() { StopPatrol(); }

    // ---------------- Initialization ----------------

    private IEnumerator InitializeAndStart() {
        yield return null;

        for (int i = 0; i < 8 && !_ready; i++) {
            AcquireAbilities();
            _ready = ValidateSetup(logIfMissing: i == 7);
            if (!_ready) yield return null;
        }

        if (!_ready) yield break;

        if (startOnEnable) StartPatrol();
    }

    private void AcquireAbilities() {
        if (_ucl == null) return;
        if (_pathfind == null)    _pathfind = _ucl.GetAbility<PathfindingMovement>();
        if (_navMove == null)     _navMove  = _ucl.GetAbility<NavMeshAgentMovement>();
        if (_agentMove == null)   _agentMove= _ucl.GetAbility<AgentMovement>();
        if (_nav == null && _navMove != null) _nav = GetComponent<NavMeshAgent>();
    }

    private bool ValidateSetup(bool logIfMissing) {
        if (_ucl == null) {
            if (logIfMissing) Debug.LogError($"{name}: UltimateCharacterLocomotion is missing.");
            return false;
        }
        if (_pathfind == null) {
            if (logIfMissing) Debug.LogError($"{name}: PathfindingMovement ability is missing. Add it to the character.");
            return false;
        }
        if (_navMove == null && _agentMove == null) {
            if (logIfMissing) Debug.LogError($"{name}: Neither NavMeshAgentMovement nor AgentMovement ability found. Add one.");
            return false;
        }
        if (_navMove != null && _nav == null) {
            if (logIfMissing) Debug.LogError($"{name}: NavMeshAgentMovement is present but no NavMeshAgent component found.");
            return false;
        }
        return true;
    }

    // ---------------- Public control ----------------

    public void StartPatrol() {
        if (!_ready) { _ = StartCoroutine(InitializeAndStart()); return; }
        if (_routine != null) return;

        if (waypoints == null || waypoints.Length == 0) {
            Debug.LogWarning($"{name}: No patrol points assigned.");
            return;
        }

        _index = 0;
        _paused = false;

        if (!_pathfind.Enabled) _pathfind.Enabled = true;

        _routine = StartCoroutine(PatrolLoop());
    }

    public void StopPatrol() {
        if (_routine != null) {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    public void Pause(bool pause) {
        _paused = pause;
        if (!_ready || _pathfind == null) return;

        if (pause) {
            _pathfind.SetDestination(transform.position);
        } else {
            if (waypoints != null && waypoints.Length > 0 && waypoints[_index] != null) {
                _pathfind.SetDestination(waypoints[_index].position);
            }
        }
    }

    // ---------------- Patrol loop ----------------

    private IEnumerator PatrolLoop() {
        if (waypoints[_index] != null) _pathfind.SetDestination(waypoints[_index].position);

        while (enabled) {
            if (_paused || !_ready || _pathfind == null) { yield return null; continue; }

            if (waypoints[_index] == null) {
                Advance();
                if (waypoints[_index] != null) _pathfind.SetDestination(waypoints[_index].position);
                yield return null;
                continue;
            }

            float dist = Vector3.Distance(transform.position, waypoints[_index].position);
            if (dist <= arriveDistance) {
                if (waitAtPoint > 0f) yield return new WaitForSeconds(waitAtPoint);

                Advance();

                if (waypoints[_index] != null)
                    _pathfind.SetDestination(waypoints[_index].position);
            }

            yield return null;
        }
    }

    private void Advance() {
        if (waypoints.Length <= 1) return;
        _index = loop ? (_index + 1) % waypoints.Length
                      : Mathf.Min(_index + 1, waypoints.Length - 1);
    }

    // ---------------- Gizmos ----------------
    private void OnDrawGizmosSelected() {
        if (!drawGizmos || waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = pointColor;
        for (int i = 0; i < waypoints.Length; i++) {
            var t = waypoints[i];
            if (t == null) continue;
            Gizmos.DrawSphere(t.position, gizmoPointRadius);

            if (i < waypoints.Length - 1 && waypoints[i + 1] != null) {
                Gizmos.color = lineColor;
                Gizmos.DrawLine(t.position, waypoints[i + 1].position);
                Gizmos.color = pointColor;
            }
        }

        if (loop && waypoints.Length > 1 && waypoints[0] && waypoints[^1]) {
            Gizmos.color = lineColor;
            Gizmos.DrawLine(waypoints[^1].position, waypoints[0].position);
        }
    }
}
