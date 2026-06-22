using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Enemy behavior #3 — platform hopper. Walks to the nearest platform or
    /// rock, jumps on top of it, attacks three times, then moves on to the
    /// next one and repeats.
    /// </summary>
    public class PlatformHopperEnemy : MonoBehaviour, IStunnable
    {
        // Yaw angles that face the model toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;

        [SerializeField] private float moveSpeed = 3f;
        [Tooltip("Seconds the hop arc takes.")]
        [SerializeField] private float jumpDuration = 0.5f;
        [Tooltip("Apex height added to the hop arc.")]
        [SerializeField] private float jumpHeight = 1.5f;
        [Tooltip("Horizontal distance at which the hopper is 'at' its target.")]
        [SerializeField] private float arriveThreshold = 0.5f;
        [SerializeField] private int attacksPerTarget = 3;
        [SerializeField] private float attackInterval = 0.6f;
        [SerializeField] private int attackDamage = 2;
        [Tooltip("The Paladin takes damage from an attack only if within this distance.")]
        [SerializeField] private float attackReach = 2.5f;

        private enum State { Seek, MoveToTarget, JumpUp, Attack, JumpDown }

        private Animator  _animator;
        private Health    _ownHealth;
        private Health    _playerHealth;
        private Transform _player;
        private bool  _facingRight;
        private float _stunnedUntil;

        private State     _state = State.Seek;
        private Transform _target;
        private Transform _lastTarget;
        private int   _attacksDone;
        private float _nextAttackTime;

        private Vector3 _jumpFrom, _jumpTo;
        private float   _jumpStartTime;

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

            switch (_state)
            {
                case State.Seek:         TickSeek();                  break;
                case State.MoveToTarget: TickMoveToTarget();          break;
                case State.JumpUp:       TickJump(State.Attack);      break;
                case State.Attack:       TickAttack();                break;
                case State.JumpDown:     TickJump(State.Seek);        break;
            }
        }

        private void TickSeek()
        {
            _target = FindNearestTarget();
            SetMoveAnim(0f);
            if (_target != null) _state = State.MoveToTarget;
        }

        private void TickMoveToTarget()
        {
            if (_target == null) { _state = State.Seek; return; }

            float dx = _target.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= arriveThreshold)
            {
                // Begin the hop onto the target's top surface.
                _jumpFrom = transform.position;
                _jumpTo   = new Vector3(_target.position.x, TargetTopY(_target), transform.position.z);
                _jumpStartTime = Time.time;
                if (_animator != null) _animator.SetTrigger("Jump");
                SetMoveAnim(0f);
                _state = State.JumpUp;
                return;
            }

            int dir = dx > 0f ? 1 : -1;
            transform.position += Vector3.right * (dir * moveSpeed * Time.deltaTime);
            FaceDir(dir > 0);
            SetMoveAnim(1f);
        }

        private void TickJump(State next)
        {
            float t = jumpDuration > 0f ? (Time.time - _jumpStartTime) / jumpDuration : 1f;
            if (t >= 1f)
            {
                transform.position = _jumpTo;
                if (next == State.Attack)
                {
                    _attacksDone = 0;
                    _nextAttackTime = Time.time;
                }
                else
                {
                    _lastTarget = _target;
                    _target = null;
                }
                _state = next;
                return;
            }

            // Straight-line lerp plus a parabolic arc (apex at t = 0.5).
            Vector3 pos = Vector3.Lerp(_jumpFrom, _jumpTo, t);
            pos.y += jumpHeight * 4f * t * (1f - t);
            transform.position = pos;
        }

        private void TickAttack()
        {
            SetMoveAnim(0f);
            if (_player != null) FaceDir(_player.position.x > transform.position.x);

            if (Time.time < _nextAttackTime) return;

            // Fire one punch.
            if (_animator != null) _animator.SetTrigger("Attack");
            if (_player != null && _playerHealth != null)
            {
                bool inReach = Mathf.Abs(_player.position.x - transform.position.x) <= attackReach
                            && Mathf.Abs(_player.position.y - transform.position.y) <= attackReach;
                if (inReach) _playerHealth.TakeDamage(attackDamage);
            }
            _attacksDone++;
            _nextAttackTime = Time.time + attackInterval;

            if (_attacksDone >= attacksPerTarget)
            {
                // Hop back down to ground level, then seek the next target.
                _jumpFrom = transform.position;
                _jumpTo   = new Vector3(transform.position.x, 0f, transform.position.z);
                _jumpStartTime = Time.time;
                if (_animator != null) _animator.SetTrigger("Jump");
                _state = State.JumpDown;
            }
        }

        /// <summary>
        /// Nearest obstacle (rock/platform) other than the one just visited.
        /// Obstacles are the children of the scene's "Obstacles" container.
        /// </summary>
        private Transform FindNearestTarget()
        {
            var root = GameObject.Find("Obstacles");
            if (root == null) return _lastTarget;

            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (Transform child in root.transform)
            {
                if (child == _lastTarget) continue;
                float d = Mathf.Abs(child.position.x - transform.position.x);
                if (d < bestDist) { bestDist = d; best = child; }
            }
            // If the last target was the only option, allow revisiting it.
            return best != null ? best : _lastTarget;
        }

        private static float TargetTopY(Transform t)
        {
            var col = t.GetComponent<Collider>();
            if (col != null) return col.bounds.max.y;
            var rend = t.GetComponent<Renderer>();
            if (rend != null) return rend.bounds.max.y;
            return t.position.y;
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
