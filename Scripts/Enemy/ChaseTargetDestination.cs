using UnityEngine;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities.AI;

public class GateChaseByRange : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private string playerTag = "Player";

    [Header("Ranges (meters)")]
    [Tooltip("Approx distance at which your melee can reliably hit (use your MeleeAgent TargetDistance, weapon range, etc.).")]
    [SerializeField] private float attackRange = 2.2f;
    [Tooltip("Prevents jitter by using two thresholds: start chase when farther than attackRange + hysteresis; stop chase when closer than attackRange - hysteresis.")]
    [SerializeField] private float hysteresis = 0.4f;
    [Tooltip("Where to stop relative to the target when chasing (keeps a little gap).")]
    [SerializeField] private float stopDistance = 1.6f;

    private UltimateCharacterLocomotion _ucl;
    private PathfindingMovement _move;
    private LocalLookSource _look;
    private Transform _target;

    void Awake() {
        _ucl  = GetComponent<UltimateCharacterLocomotion>();
        _move = _ucl.GetAbility<PathfindingMovement>();
        _look = GetComponent<LocalLookSource>();
    }

    System.Collections.IEnumerator Start() {
        yield return null;

        if (_target == null) {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) _target = player.transform;
        }

        if (_look != null && _target != null && _look.Target == null) {
            _look.Target = _target;
        }
    }

    void Update() {
        if (_move == null || _target == null) return;

        float startChaseDist = attackRange + Mathf.Max(0f, hysteresis);
        float stopChaseDist  = Mathf.Max(0.01f, attackRange - Mathf.Max(0f, hysteresis));

        float dist = Vector3.Distance(transform.position, _target.position);

        if (dist > startChaseDist) {
            if (!_move.Enabled) _move.Enabled = true;

            var dir  = (_target.position - transform.position).normalized;
            var dest = _target.position - dir * stopDistance;
            _move.SetDestination(dest);
        } else if (dist < stopChaseDist) {
            if (_move.Enabled) _move.Enabled = false;
        }
    }

    public void SetTarget(Transform t) {
        _target = t;
        if (_look != null) _look.Target = t;
    }
}
