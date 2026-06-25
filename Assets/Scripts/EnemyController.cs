using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Patrolling enemy behavior:
    ///   • Patrol — walks back and forth around its spawn X.
    ///   • Chase  — when the player enters detectionRange, walks toward them.
    ///   • Attack — when the player enters attackRange, stops, faces them and
    ///              punches on a cooldown.
    /// Movement is kinematic (transform-based) on the flat ground plane.
    /// </summary>
    public class EnemyController : MonoBehaviour, IStunnable
    {
        // Yaw angles that face the model toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;

        [Header("Patrol")]
        [Tooltip("How far the enemy wanders each way from its spawn X.")]
        [SerializeField] private float patrolRange = 5f;
        [SerializeField] private float patrolSpeed = 1.5f;

        [Header("Engage")]
        [Tooltip("Player within this X-distance: the enemy chases.")]
        [SerializeField] private float detectionRange = 6f;
        [Tooltip("Player within this X-distance: the enemy stops and attacks.")]
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float chaseSpeed = 2.5f;
        [Tooltip("Seconds between attacks.")]
        [SerializeField] private float attackCooldown = 1.5f;
        [Tooltip("Damage each punch deals to the player.")]
        [SerializeField] private int attackDamage = 2;

        // Animator parameter hashes.
        static readonly int HashSpeed  = Animator.StringToHash("Speed");
        static readonly int HashAttack = Animator.StringToHash("Attack");

        Animator _animator;
        NetworkAnimator _netAnimator;   // replicates the punch trigger to clients
        Health   _ownHealth;
        Health   _playerHealth;         // nearest living player's Health (host-side)
        Transform _target;              // that player's transform
        float _patrolMinX, _patrolMaxX;
        int   _patrolDir = -1;          // start heading left (toward the Paladin)
        float _lastAttackTime = -999f;
        bool  _facingRight;
        float _stunnedUntil;            // Time.time the current stun ends

        /// <summary>Stun the enemy: it stops moving/attacking for the duration.</summary>
        public void ApplyStun(float duration)
        {
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + duration);
        }

        public bool IsStunned => Time.time < _stunnedUntil;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _netAnimator = GetComponent<NetworkAnimator>();
            float spawnX = transform.position.x;
            _patrolMinX = spawnX - patrolRange;
            _patrolMaxX = spawnX + patrolRange;
            // Derive initial facing from however the enemy was spawned/rotated
            // (the spawner faces new enemies toward the Paladin).
            _facingRight = transform.forward.x >= 0f;

            // Despawn the enemy when its Health hits zero.
            _ownHealth = GetComponent<Health>();
            if (_ownHealth != null) _ownHealth.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_ownHealth != null) _ownHealth.Died -= OnDied;
        }

        private void OnDied(Health h)
        {
            // This behaviour only ever runs on the host, so despawn (which also
            // destroys) the networked enemy across all clients.
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn();
            else Destroy(gameObject);
        }

        private void Update()
        {
            // While stunned the enemy holds still and takes no action.
            if (IsStunned)
            {
                SetAnimSpeed(0f);
                return;
            }

            // Re-acquire the nearest living player every frame (co-op: up to two;
            // downed players are skipped).
            _playerHealth = PlayerRegistry.GetNearestLiving(transform.position);
            _target = _playerHealth != null ? _playerHealth.transform : null;

            float dist = _target != null
                ? Mathf.Abs(_target.position.x - transform.position.x)
                : Mathf.Infinity;

            if (dist <= attackRange)         AttackState();
            else if (dist <= detectionRange) ChaseState();
            else                             PatrolState();
        }

        private void PatrolState()
        {
            float x = transform.position.x;
            if (x >= _patrolMaxX)      _patrolDir = -1;
            else if (x <= _patrolMinX) _patrolDir = 1;

            MoveX(_patrolDir, patrolSpeed);
            FaceDir(_patrolDir > 0);
            SetAnimSpeed(1f);
        }

        private void ChaseState()
        {
            int dir = _target.position.x > transform.position.x ? 1 : -1;
            MoveX(dir, chaseSpeed);
            FaceDir(dir > 0);
            SetAnimSpeed(1f);
        }

        private void AttackState()
        {
            // Hold position, face the player, punch on cooldown.
            SetAnimSpeed(0f);
            FaceDir(_target.position.x > transform.position.x);

            if (Time.time >= _lastAttackTime + attackCooldown)
            {
                _lastAttackTime = Time.time;
                TriggerAttack();
                // Runs on the host; player Health is server-authoritative.
                if (_playerHealth != null) _playerHealth.TakeDamage(attackDamage);
            }
        }

        // Fire the punch through the NetworkAnimator so the swing replicates to
        // clients (the host owns the enemy and drives its animator).
        private void TriggerAttack()
        {
            if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
            else if (_animator != null) _animator.SetTrigger(HashAttack);
        }

        private void MoveX(int dir, float speed)
        {
            transform.position += Vector3.right * (dir * speed * Time.deltaTime);
        }

        private void FaceDir(bool faceRight)
        {
            if (faceRight == _facingRight) return;
            _facingRight = faceRight;
            // Model forward is +Z; +90° on Y faces +X (right), -90° faces -X.
            transform.rotation = Quaternion.Euler(0f, faceRight ? FacingRightYaw : FacingLeftYaw, 0f);
        }

        private void SetAnimSpeed(float v)
        {
            if (_animator != null) _animator.SetFloat(HashSpeed, v);
        }

        // Editor-only range visualization.
        private void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eye, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(eye, attackRange);

            Gizmos.color = Color.cyan;
            float centerX = Application.isPlaying
                ? (_patrolMinX + _patrolMaxX) * 0.5f
                : transform.position.x;
            float y = transform.position.y + 0.1f;
            float z = transform.position.z;
            Gizmos.DrawLine(new Vector3(centerX - patrolRange, y, z),
                            new Vector3(centerX + patrolRange, y, z));
        }
    }
}
