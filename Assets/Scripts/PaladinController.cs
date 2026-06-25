using UnityEngine;
using UnityEngine.InputSystem;

namespace DgProto
{
    /// <summary>
    /// Sidescroller controller for the Paladin.
    /// Controls:
    ///   A / D            - move left / right (flips facing)
    ///   Space            - jump
    ///   Q                - normal attack combo: NormalAttack → Backslash → Kick
    ///   E                - special attack combo: SpecialAttack → Thrust
    ///   Left Ctrl (hold) - crouch
    ///   Ctrl + Q         - crouch attack (single, not part of a combo)
    /// Each press advances its combo step if it lands within
    /// <see cref="comboWindowDuration"/> of the previous attack ending; miss
    /// the window and the combo restarts from step 1. Input during a swing is
    /// ignored (no early-cancel).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Animator))]
    public class PaladinController : MonoBehaviour
    {
        // Yaw angles that face the model toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;
        // Far-future timestamp parked on the combo window during a swing so the
        // "missed window" reset can't fire before IsAttacking() turns true.
        private const float AttackWindowGuardSeconds = 999f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float jumpForce = 8f;

        [Header("Combat")]
        [Tooltip("Damage dealt by a normal (Q) attack and the crouch attack.")]
        [SerializeField] private int normalAttackDamage = 2;
        [Tooltip("Damage dealt by a special (E) attack.")]
        [SerializeField] private int specialAttackDamage = 3;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float meleeHeight = 2f;

        [Range(0f, 1f)]
        [Tooltip("Normalized time into the attack animation at which the hit lands (synced to the swing's contact frame).")]
        [SerializeField] private float hitNormalizedTime = 0.45f;

        [Tooltip("Seconds of stun a successful special (E) hit inflicts on an enemy.")]
        [SerializeField] private float specialStunDuration = 0.3f;

        // Pending hit for the current swing — applied at hitNormalizedTime.
        int  _pendingDamage;
        bool _hitFired;
        // True when the current pending hit is from a Special attack (used to
        // decide whether to apply the stun in DealMeleeDamage).
        bool _isSpecialSwing;

        [Tooltip("Seconds after an attack animation ends during which a press chains to the next combo step. Miss it and the combo restarts from step 1.")]
        [SerializeField] private float comboWindowDuration = 0.2f;

        // Server-authoritative melee resolver on the same networked object.
        // PaladinController computes when/where a swing connects; PlayerMelee
        // applies the damage on the host so enemy HP stays consistent.
        PlayerMelee _melee;

        // Combo state.
        //   _comboType: which combo is active (Normal / Special / None)
        //   _comboStep: 1-based index of the current move within that combo
        //   _comboWindowEnd: Time.time at which the post-animation press window closes
        const int ComboNone = 0, ComboNormal = 1, ComboSpecial = 2;
        int   _comboType;
        int   _comboStep;
        float _comboWindowEnd;
        bool  _wasAttacking;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;        // optional; falls back to a sphere at the feet
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private LayerMask groundMask = ~0;    // everything by default

        // Reusable buffer so the ground check allocates nothing per frame.
        readonly Collider[] _groundHits = new Collider[8];

        // Animator parameter hashes
        static readonly int HashSpeed         = Animator.StringToHash("Speed");
        static readonly int HashIsGrounded    = Animator.StringToHash("IsGrounded");
        static readonly int HashIsCrouching   = Animator.StringToHash("IsCrouching");
        static readonly int HashJump          = Animator.StringToHash("Jump");
        static readonly int HashNormalAttack  = Animator.StringToHash("NormalAttack");
        static readonly int HashSpecialAttack = Animator.StringToHash("SpecialAttack");
        static readonly int HashCrouchAttack  = Animator.StringToHash("CrouchAttack");
        static readonly int HashBackslash     = Animator.StringToHash("Backslash");
        static readonly int HashKick          = Animator.StringToHash("Kick");
        static readonly int HashThrust        = Animator.StringToHash("Thrust");

        // Animator state name hashes — used to detect "is in an attack state".
        // Animator.StringToHash on a bare state name matches AnimatorStateInfo.shortNameHash.
        static readonly int[] AttackStateHashes =
        {
            Animator.StringToHash("NormalAttack"),
            Animator.StringToHash("SpecialAttack"),
            Animator.StringToHash("CrouchAttack"),
            Animator.StringToHash("Backslash"),
            Animator.StringToHash("Kick"),
            Animator.StringToHash("Thrust"),
        };

        Rigidbody _rb;
        Animator _animator;
        Health _health;
        bool _isGrounded;
        bool _wasGrounded;            // previous frame's _isGrounded — used to detect "Land"
        float _airborneSince;         // Time.time when _isGrounded last went false → true; used to suppress micro-bounce land SFX
        bool _facingRight = true;
        bool _dead;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _health = GetComponent<Health>();
            _melee = GetComponent<PlayerMelee>();
            if (_health != null)
            {
                _health.Died += OnPlayerDied;
                _health.Changed += OnPlayerHealthChanged;
            }

            // Lock to the X-Y plane for sidescroller motion.
            _rb.constraints =
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Face right initially.
            transform.rotation = Quaternion.Euler(0f, FacingRightYaw, 0f);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnPlayerDied;
                _health.Changed -= OnPlayerHealthChanged;
            }
        }

        // Tracks last-seen HP so we can fire PlayerHurt only when HP decreased
        // (Health.Changed also fires on heal).
        private int _lastSeenHP = int.MinValue;
        private void OnPlayerHealthChanged(Health h)
        {
            if (_lastSeenHP != int.MinValue && h.CurrentHP < _lastSeenHP)
            {
                AudioManager.Instance.Play(SfxId.PlayerHurt);
            }
            _lastSeenHP = h.CurrentHP;
        }

        private void OnPlayerDied(Health h)
        {
            // This Paladin is down: stop input/movement and let it lie. In co-op
            // a single death does NOT end the match or show the game-over screen —
            // the teammate plays on. Detecting "both Paladins down" → match end +
            // GameOverScreen is owned by the match lifecycle (Milestone 5).
            _dead = true;
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (_dead) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // --- Ground check ---
            // OverlapSphere at the feet, ignoring our own colliders. Avoids the
            // self-overlap problem of SphereCast that starts inside the capsule.
            Vector3 origin = groundCheck != null ? groundCheck.position : transform.position + groundCheckOffset;
            int n = Physics.OverlapSphereNonAlloc(origin, groundCheckRadius, _groundHits, groundMask, QueryTriggerInteraction.Ignore);
            _isGrounded = false;
            for (int i = 0; i < n; i++)
            {
                var col = _groundHits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                _isGrounded = true;
                break;
            }

            // Land SFX: false→true transition this frame, but only if we were
            // airborne for more than 0.2 s (suppresses single-frame micro-bounces
            // when standing on the ground-check seam).
            if (_isGrounded && !_wasGrounded)
            {
                if (Time.time - _airborneSince > 0.2f)
                {
                    AudioManager.Instance.Play(SfxId.Land);
                }
            }
            else if (!_isGrounded && _wasGrounded)
            {
                _airborneSince = Time.time;
            }
            _wasGrounded = _isGrounded;

            // --- Crouch (hold) ---
            bool crouchHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            _animator.SetBool(HashIsCrouching, crouchHeld && _isGrounded);

            // --- Attack lockout ---
            // While an attack animation is playing (or transitioning to/from one)
            // we freeze horizontal movement, jumping, and facing flips.
            bool attacking = IsAttacking();

            // --- Combo window bookkeeping ---
            // The moment an attack animation finishes, open the chain window.
            // If it lapses with no press, the combo resets to step 1.
            if (_wasAttacking && !attacking && _comboType != ComboNone)
            {
                _comboWindowEnd = Time.time + comboWindowDuration;
            }
            _wasAttacking = attacking;

            if (_comboType != ComboNone && !attacking && Time.time > _comboWindowEnd)
            {
                _comboType = ComboNone;
                _comboStep = 0;
            }

            // --- Horizontal movement ---
            float h = 0f;
            if (!attacking)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (crouchHeld) h = 0f; // no walking while crouching
            }

            Vector3 v = _rb.linearVelocity;
            v.x = h * moveSpeed;
            _rb.linearVelocity = v;

            _animator.SetFloat(HashSpeed, Mathf.Abs(h));
            _animator.SetBool(HashIsGrounded, _isGrounded);

            // --- Flip facing (only if not attacking) ---
            if (!attacking)
            {
                if (h > 0.01f && !_facingRight) Flip(true);
                else if (h < -0.01f && _facingRight) Flip(false);
            }

            // --- Jump (only if grounded, not crouching, not attacking) ---
            if (kb.spaceKey.wasPressedThisFrame && _isGrounded && !crouchHeld && !attacking)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, 0f);
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                _animator.SetTrigger(HashJump);
                AudioManager.Instance.Play(SfxId.Jump);
            }

            // --- Attacks & combos ---
            // Presses during a swing are ignored. A press made within the
            // window AFTER an animation ends chains to the next combo step;
            // a press outside any window restarts the combo from step 1.
            if (kb.qKey.wasPressedThisFrame && !attacking)
            {
                if (crouchHeld)
                {
                    // Crouch attack — single move, not part of either combo.
                    _comboType = ComboNone;
                    _comboStep = 0;
                    ResetAttackTriggers();
                    _animator.SetTrigger(HashCrouchAttack);
                    AudioManager.Instance.Play(SfxId.AttackSwing);
                    BeginAttack(normalAttackDamage, isSpecial: false);
                }
                else
                {
                    bool chaining = _comboType == ComboNormal && Time.time <= _comboWindowEnd;
                    _comboStep = chaining ? _comboStep + 1 : 1;
                    if (_comboStep > 3) _comboStep = 1; // wrap past Kick
                    _comboType = ComboNormal;
                    FireComboMove();
                }
            }
            if (kb.eKey.wasPressedThisFrame && !crouchHeld && !attacking)
            {
                bool chaining = _comboType == ComboSpecial && Time.time <= _comboWindowEnd;
                _comboStep = chaining ? _comboStep + 1 : 1;
                if (_comboStep > 2) _comboStep = 1; // wrap past Thrust
                _comboType = ComboSpecial;
                FireComboMove();
            }

            // --- Hit timing ---
            // Apply the pending hit when the swing reaches its contact frame,
            // rather than the instant the button was pressed.
            if (attacking && !_hitFired && !_animator.IsInTransition(0))
            {
                float nt = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                if (nt >= hitNormalizedTime)
                {
                    DealMeleeDamage(_pendingDamage);
                    _hitFired = true;
                }
            }
        }

        /// <summary>
        /// Arms the pending hit for the swing that just started. The damage is
        /// applied later, in Update, once the animation reaches hitNormalizedTime.
        /// <paramref name="isSpecial"/> remembers whether this swing should also
        /// apply <see cref="specialStunDuration"/> on hit.
        /// </summary>
        private void BeginAttack(int damage, bool isSpecial)
        {
            _pendingDamage  = damage;
            _isSpecialSwing = isSpecial;
            _hitFired       = false;
        }

        /// <summary>
        /// Fires the Animator trigger for the current combo step, plays the
        /// matching SFX, and arms the pending hit. Pushes the combo window far
        /// out so the "missed window" reset can't fire in the 1-frame gap
        /// before IsAttacking() turns true; the real window is set when this
        /// animation ends.
        /// </summary>
        private void FireComboMove()
        {
            ResetAttackTriggers();
            int trigger = HashNormalAttack;
            SfxId swing = SfxId.AttackSwing;
            if (_comboType == ComboNormal)
            {
                switch (_comboStep)
                {
                    case 1: trigger = HashNormalAttack; swing = SfxId.AttackSwing; break;
                    case 2: trigger = HashBackslash;    swing = SfxId.AttackSwing; break;
                    case 3: trigger = HashKick;         swing = SfxId.Kick;        break;
                }
            }
            else if (_comboType == ComboSpecial)
            {
                switch (_comboStep)
                {
                    case 1: trigger = HashSpecialAttack; swing = SfxId.AttackSwing; break;
                    case 2: trigger = HashThrust;        swing = SfxId.Stab;        break;
                }
            }
            _animator.SetTrigger(trigger);
            AudioManager.Instance.Play(swing);
            _comboWindowEnd = Time.time + AttackWindowGuardSeconds;
            BeginAttack(_comboType == ComboSpecial ? specialAttackDamage : normalAttackDamage,
                        isSpecial: _comboType == ComboSpecial);
        }

        /// <summary>
        /// Clears every attack trigger so a stale, unconsumed trigger can't
        /// fire an unintended transition on the next combo step.
        /// </summary>
        private void ResetAttackTriggers()
        {
            _animator.ResetTrigger(HashNormalAttack);
            _animator.ResetTrigger(HashSpecialAttack);
            _animator.ResetTrigger(HashCrouchAttack);
            _animator.ResetTrigger(HashBackslash);
            _animator.ResetTrigger(HashKick);
            _animator.ResetTrigger(HashThrust);
        }

        /// <summary>
        /// Computes the swing box in front of the Paladin and asks
        /// <see cref="PlayerMelee"/> to resolve it on the host. The overlap test
        /// and damage application happen server-side so enemy HP is authoritative;
        /// special (E) swings also carry a stun. Only the owning client reaches
        /// this (its controller is the only one enabled).
        /// </summary>
        private void DealMeleeDamage(int amount)
        {
            Vector3 facingDir = _facingRight ? Vector3.right : Vector3.left;
            Vector3 center = transform.position
                           + Vector3.up * (meleeHeight * 0.5f)
                           + facingDir * (meleeRange * 0.5f);
            Vector3 halfExtents = new Vector3(meleeRange * 0.5f, meleeHeight * 0.5f, 1f);

            float stunDuration = _isSpecialSwing ? specialStunDuration : 0f;
            if (_melee != null) _melee.RequestHit(center, halfExtents, amount, stunDuration);
        }

        /// <summary>
        /// True while the Animator is in (or transitioning into) any of the
        /// attack states. Used to lock out movement, jump, and facing flips.
        /// </summary>
        private bool IsAttacking()
        {
            if (IsAttackState(_animator.GetCurrentAnimatorStateInfo(0).shortNameHash)) return true;

            if (_animator.IsInTransition(0) &&
                IsAttackState(_animator.GetNextAnimatorStateInfo(0).shortNameHash)) return true;

            return false;
        }

        private static bool IsAttackState(int hash)
        {
            for (int i = 0; i < AttackStateHashes.Length; i++)
                if (AttackStateHashes[i] == hash) return true;
            return false;
        }

        /// <summary>
        /// Invoked by AnimationEvents placed on each foot-strike frame of the
        /// Paladin's Walk clip. Suppresses footsteps when airborne or otherwise
        /// not actually moving.
        /// </summary>
        public void OnFootstep()
        {
            if (!_isGrounded) return;
            if (Mathf.Abs(_rb.linearVelocity.x) < 0.1f) return;
            AudioManager.Instance.Play(SfxId.Footstep);
        }

        private void Flip(bool facingRight)
        {
            _facingRight = facingRight;
            // Write through the Rigidbody so the physics engine stays in sync.
            // Setting transform.rotation alone can be reverted by the next physics
            // tick when Interpolate caches the previous rotation, which causes
            // the flip to "stick" on rapid A↔D switches.
            var rot = Quaternion.Euler(0f, facingRight ? FacingRightYaw : FacingLeftYaw, 0f);
            _rb.rotation = rot;
            _rb.MoveRotation(rot);
            transform.rotation = rot;
        }
    }
}
