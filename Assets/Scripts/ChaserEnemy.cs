using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Enemy behavior #2 — pure chaser. Always walks straight toward the
    /// Paladin and attacks once it is within range. No patrol phase.
    /// </summary>
    public class ChaserEnemy : MonoBehaviour, IStunnable
    {
        // Yaw angles that face the model toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;

        [SerializeField] private float chaseSpeed = 2.8f;
        [Tooltip("Player within this X-distance: stop and attack.")]
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private int attackDamage = 2;

        private Animator  _animator;
        private Health    _ownHealth;
        private Health    _playerHealth;
        private Transform _player;
        private bool  _facingRight;
        private float _lastAttackTime = -999f;
        private float _stunnedUntil;

        public void ApplyStun(float duration)
        {
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + duration);
        }

        public bool IsStunned => Time.time < _stunnedUntil;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _ownHealth = GetComponent<Health>();
            if (_ownHealth != null) _ownHealth.Died += OnDied;
            _facingRight = transform.forward.x >= 0f;
        }

        private void Start()
        {
            var pc = Object.FindAnyObjectByType<PaladinController>();
            if (pc != null)
            {
                _player = pc.transform;
                _playerHealth = pc.GetComponent<Health>();
            }
        }

        private void OnDestroy()
        {
            if (_ownHealth != null) _ownHealth.Died -= OnDied;
        }

        private void OnDied(Health h) => Destroy(gameObject);

        private void Update()
        {
            if (IsStunned) { SetMoveAnim(0f); return; }
            if (_player == null) { SetMoveAnim(0f); return; }

            float dx = _player.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= attackRange)
            {
                // In range — hold position and attack on cooldown.
                SetMoveAnim(0f);
                FaceDir(dx > 0f);
                if (Time.time >= _lastAttackTime + attackCooldown)
                {
                    _lastAttackTime = Time.time;
                    if (_animator != null) _animator.SetTrigger("Attack");
                    if (_playerHealth != null) _playerHealth.TakeDamage(attackDamage);
                }
            }
            else
            {
                // Chase.
                int dir = dx > 0f ? 1 : -1;
                transform.position += Vector3.right * (dir * chaseSpeed * Time.deltaTime);
                FaceDir(dir > 0);
                SetMoveAnim(1f);
            }
        }

        private void SetMoveAnim(float speed)
        {
            if (_animator != null) _animator.SetFloat("Speed", speed);
        }

        private void FaceDir(bool faceRight)
        {
            if (faceRight == _facingRight) return;
            _facingRight = faceRight;
            transform.rotation = Quaternion.Euler(0f, faceRight ? FacingRightYaw : FacingLeftYaw, 0f);
        }
    }
}
