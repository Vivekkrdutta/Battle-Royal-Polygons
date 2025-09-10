using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using Random = UnityEngine.Random;

namespace AI
{
    public class SmartAgentBehaviour : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float acceleration = 8f;
        [SerializeField] private float rotationSlerpSpeed = 6f;
        [SerializeField] private float stoppingDistanceLerpSpeed = 10f;
        [SerializeField] private AudioClip[] FootstepAudioClips;

        [Header("Distances")]
        [SerializeField] private float minDistanceToStartFire = 15f;
        [SerializeField] private float standerdDistanceToMaintainFromTarget = 8f;
        [SerializeField] private float minDistanceToMaintainFromTarget = 3f;

        [Header("Timers")]
        [SerializeField] private float checkTargetPositionTimer = 0.5f;   // how often we reconsider SetDestination
        [SerializeField] private float distanceRepathThreshold = 1.0f;     // how far target moved before repath
        [SerializeField] private float notHittingTimeMax = 0.5f;
        [SerializeField] private float forceRepathTimerMax = 3f;
        [SerializeField] private float newTargetTimerMax = 5f; // how fast the player keeps looking for a new target

        [Header("Combat")]
        [SerializeField] private LayerMask aimLayerMask;
        [SerializeField] private Rig rigs;
        [SerializeField] private MultiAimConstraint[] aimConstraints;
        [SerializeField] private Transform leftHantIkTarget; // for equipping guns

        [Header("Guns details")]
        [SerializeField] private GunPropertiesOverride gunPropertiesEasyLevel;
        [SerializeField] private GunPropertiesOverride gunPropertiesMediumLevel;
        [SerializeField] private GunPropertiesOverride gunPropertiesHardLevel;

        [Serializable]
        public class GunPropertiesOverride
        {
            public float DamageAmount = 10;
            public float Range = 20;
            public float RateOfFire = 2;
            public int AmmoCapacity = 10;
            public float ReloadTime = 1.5f;
            [Range(0f,1f)] public float HitProbability = 0.7f;
            public FireArm FireArmPrefab;
            public float maxHealth = 1f;
        }

        // Animation IDs
        private int _animIDAim;
        private int _animIDSpeed;
        private int _animIDSpeedX;
        private int _animIDSpeedY;
        private int _animIDReload;

        // State
        private bool _isAiming = false;
        private bool _isReloading = false;
        private bool _isAlive = true;

        private float fireTimer = 0f;
        private float reloadTimer = 0f;
        private float repathTimer = 0f;
        private float notHittingTimer = 0f;
        private float forceRepathTimer = 0f;
        private float hitProbability = 0.9f;
        private float newTargetTimer = 0f;
        
        private float activeDistanceToMaintainFromTarget = 0f;

        private int currentBulletCount;

        private NavMeshAgent agent;
        private Animator animator;
        private FireArm activeFireArm;
        private Transform aimTarget;     // where we aim at (from ITargetable)
        private Transform targetShooter; // shooter root we try to hit
        private Vector3 prevTargetPosition = Vector3.zero;
        private AudioSource audioSource;
        private GunPropertiesOverride gunProperties;

        /// <summary>
        /// Single networked weight drives all aim constraints.
        /// Server writes, everyone reads — deterministic on clients.
        /// </summary>
        private readonly NetworkVariable<float> constraintWeight = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server // server authoritative
        );

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();

            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = standerdDistanceToMaintainFromTarget;
            agent.updateRotation = true; // will toggle off when we manually rotate during aim

            _animIDAim = Animator.StringToHash("Aim");
            _animIDSpeedX = Animator.StringToHash("SpeedX");
            _animIDSpeedY = Animator.StringToHash("SpeedY");
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDReload = Animator.StringToHash("Reload");

            activeDistanceToMaintainFromTarget = standerdDistanceToMaintainFromTarget;

            SetupGun();
            // setup the two bone ik
            SetLeftHandIk();

            GameManager.OnMatchEnded += GameManager_OnMatchEnded;
        }

        private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
        {
            GameManager.OnMatchEnded -= GameManager_OnMatchEnded;

            _isAlive = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            AgentsManager.Instance.RequestForDeploymentOfAgent(gameObject);

            PlayerShootable.OnAnyPlayerDead += PlayerShootable_OnAnyPlayerDead;

            GetComponent<SmartAgentShootable>().SetMaxHealth(gunProperties.maxHealth);

            GameMultiPlayer.Instance.OnPlayerDisconnected += GameMultiplayer_OnPlayerDisconnected;
        }

        private void GameMultiplayer_OnPlayerDisconnected(object sender, ulong e)
        {
            if (!IsServer) return;

            if (targetShooter == null) // he was targetting him.
            {
                DisableAimConstraintsClientRpc();
                gameObject.SetActive(false);
                gameObject.SetActive(true);

                AgentsManager.Instance.RequestForDeploymentOfAgent(gameObject);
            }
        }

        private void PlayerShootable_OnAnyPlayerDead(object sender, PlayerShootable.OnAnyPlayerDeadEventArgs e)
        {
            if (!IsServer) return;

            if(e.Player.transform == targetShooter)
            {
                DisableAimConstraintsClientRpc();
                gameObject.SetActive(false);
                gameObject.SetActive(true);

                AgentsManager.Instance.RequestForDeploymentOfAgent(gameObject);
            }
        }

        private void Update()
        {
            // Clients: we only drive visuals in LateUpdate; logic stays on server to avoid divergent states
            if (!IsServer) return;

            // Smoothly adapt stopping distance only if different (avoids tiny per-frame writes)
            if (Mathf.Abs(agent.stoppingDistance - activeDistanceToMaintainFromTarget) > 0.01f)
            {
                agent.stoppingDistance = Mathf.Lerp(
                    agent.stoppingDistance,
                    activeDistanceToMaintainFromTarget,
                    stoppingDistanceLerpSpeed * Time.deltaTime
                );
            }

            TickAimWeight();

            if (!_isAlive) return;

            if (_isReloading)
            {
                // keep aim off while reloading
                SetAim(false);
                DisableAgent();

                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0f)
                {
                    _isReloading = false;
                    currentBulletCount = gunProperties.AmmoCapacity;
                    ReEnableAgent();
                }
                return;
            }

            // Repath throttling
            repathTimer += Time.deltaTime; 
            forceRepathTimer += Time.deltaTime;
            if (aimTarget && repathTimer >= checkTargetPositionTimer)
            {
                var targetPos = aimTarget.position;
                if ((targetPos - prevTargetPosition).sqrMagnitude >= distanceRepathThreshold * distanceRepathThreshold)
                {
                    TrySetDestination(targetPos);
                    prevTargetPosition = targetPos;
                    forceRepathTimer = 0f;
                }
                repathTimer = 0f;
            }

            if(aimTarget && forceRepathTimer >= forceRepathTimerMax)
            {
                var targetPos = aimTarget.position;
                forceRepathTimer = 0f;
                TrySetDestination(targetPos);
                prevTargetPosition = targetPos;
            }

            HandleTarget();
        }

        private void LateUpdate()
        {
            // Apply synced aim-weight to rig on all peers
            rigs.weight = constraintWeight.Value;

            // Local animator speed drive — only server writes to avoid oscillations
            if (IsServer)
            {
                var velocity = agent.velocity;
                velocity.y = 0f;

                Vector3 localVelocity = transform.InverseTransformDirection(velocity);

                animator.SetFloat(_animIDSpeedX, localVelocity.x);
                animator.SetFloat(_animIDSpeedY, localVelocity.z);
                animator.SetFloat(_animIDSpeed, velocity.magnitude);
            }
        }

        private void SetupGun()
        {
            var shootable = GetComponent<SmartAgentShootable>();
            switch (GameProperties.GameLevel)
            {
                case GameProperties.Level.Easy:
                    gunProperties = gunPropertiesEasyLevel;
                    break;

                case GameProperties.Level.Medium:
                    gunProperties = gunPropertiesMediumLevel;
                    break;

                case GameProperties.Level.Hard:
                    gunProperties = gunPropertiesHardLevel;
                    break;
            }

            hitProbability = gunProperties.HitProbability;
            currentBulletCount = gunProperties.AmmoCapacity;
        }
        private void HandleTarget()
        {
            if (!IsServer || aimTarget == null) return;

            var distance = Vector3.Distance(transform.position, aimTarget.position);

            // If too far, move towards target and don't aim/fire
            if (distance > minDistanceToStartFire)
            {
                SetAim(false);

                newTargetTimer += Time.deltaTime;
                if(newTargetTimer > newTargetTimerMax)
                {
                    newTargetTimer = 0f;

                    if(Random.Range(0f,1f) >= 0.4f)
                    {
                        AgentsManager.Instance.RequestForDeploymentOfAgent(gameObject);
                        return;
                    }
                }

                TrySetDestination(aimTarget.position);

                return;
            }

            // While aiming we rotate manually to face target; otherwise let NavMesh rotate

            if (_isAiming)
            {
                agent.updateRotation = false;
                SmartFaceTarget(targetShooter.position);
            }

            // Hit-check (use collider center and allow any child collider on the target root)
            var nozzle = activeFireArm.GetNozzle();
            var targetCenter = GetTargetCenter(targetShooter);
            var dir = (targetCenter - nozzle.position);
            var distToTarget = dir.magnitude;
            if (distToTarget <= 0.01f) return; // degenerate
            dir /= distToTarget;

            bool hasHit = Physics.Raycast(nozzle.position, dir, out RaycastHit hit, gunProperties.Range, aimLayerMask, QueryTriggerInteraction.Ignore)
                          && hit.transform.root == targetShooter;

            if (!hasHit)
            {
                notHittingTimer += Time.deltaTime;
                if (notHittingTimer > notHittingTimeMax)
                {
                    // close the gap a bit if we’re not getting a line of sight
                    activeDistanceToMaintainFromTarget = minDistanceToMaintainFromTarget;
                    TrySetDestination(aimTarget.position);
                    SetAim(false);
                    notHittingTimer = 0f;
                }
                return;
            }

            // Within engagement range and hitting — maintain standard spacing by default
            activeDistanceToMaintainFromTarget = standerdDistanceToMaintainFromTarget;
            agent.isStopped = true;

            notHittingTimer = 0f;
            SetAim(true);

            // Fire control
            float fireDelay = 1f / Mathf.Max(0.01f, gunProperties.RateOfFire);
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f && currentBulletCount > 0)
            {
                fireTimer = fireDelay;
                FireABullet();
            }

            // Out of ammo → start reload once, not every frame
            if (currentBulletCount == 0 && !_isReloading)
            {
                _isReloading = true;
                reloadTimer = gunProperties.ReloadTime;
                animator.SetTrigger(_animIDReload);
                SetAim(false);
            }
        }

        private void SetLeftHandIk()
        {
            // instantiate the gun
            var gunsHolder = leftHantIkTarget.parent;
            activeFireArm = Instantiate(gunProperties.FireArmPrefab, gunsHolder);
            var fireArm = activeFireArm;

            // setup the orientations structure
            fireArm.SetupOrientation();
            leftHantIkTarget.SetLocalPositionAndRotation(
                fireArm.GetGunSO().LeftHandPlace.LocalPosition, 
                Quaternion.Euler(fireArm.GetGunSO().LeftHandPlace.LocalRotation)
            );
        }

        private void SetAim(bool val)
        {
            if (_isAiming == val) return;
            _isAiming = val;
            animator.SetBool(_animIDAim, val);
            agent.updateRotation = !val;
        }
        public void SetDead()
        {
            GetComponent<NavMeshAgent>().enabled = false;
            _isAlive = false;
            SetAim(false);
        }

        private void FireABullet()
        {
            currentBulletCount--;

            float range = gunProperties.Range;
            float dist = Vector3.Distance(transform.position, targetShooter.position);

            // probability scales down with distance; clamp to [0..1]
            const float bias = 0.2f;
            float probabilityMultiplier = Mathf.Clamp01(1f - dist / Mathf.Max(0.01f, range) + bias);
            float finalProb = Mathf.Clamp01(probabilityMultiplier * hitProbability);

            if (finalProb >= Random.Range(0f, 1f))
            {
                if (targetShooter.TryGetComponent(out IShootable shootable))
                {
                    shootable.AddNormalDamage(gunProperties.DamageAmount);
                }
            }

            // Visuals to everyone; AI is server-owned so RequireOwnership must be false
            FireABulletVisualClientRpc();
        }

        [ClientRpc(RequireOwnership = false)]
        private void FireABulletVisualClientRpc()
        {
            activeFireArm.GetMuzzleFlash().Play();

            var clip = activeFireArm.GetFireSound();
            audioSource.PlayOneShot(clip, 1f * GameProperties.VolumeScale);
        }

        public Vector3 GetTargetPosition() => aimTarget ? aimTarget.position : default;

        public void SetTarget(Transform target)
        {
            if (!IsServer) return;

            if (target && target.TryGetComponent(out NetworkObject netobs))
            {
                agent.enabled = true;
                animator.applyRootMotion = false;
                SetTargetClientRpc(netobs);
                return;
            }

            DisableAimConstraintsClientRpc();
        }

        [ClientRpc(RequireOwnership =  true)]
        private void DisableAimConstraintsClientRpc()
        {
            foreach (var aimConstraint in aimConstraints)
            {
                aimConstraint.data.sourceObjects = new WeightedTransformArray();
            }
        }

        // This should notify ALL clients, not just owner — AI is server-owned
        [ClientRpc(RequireOwnership = false)]
        private void SetTargetClientRpc(NetworkObjectReference targetNetRef)
        {
            if (!targetNetRef.TryGet(out NetworkObject netObj) || netObj == null)
            {
                Debug.LogError("Target was not set");
                return;
            }

            var t = netObj.transform;
            if (t == null)
            {
                return;
            }

            targetShooter = t;

            if (!targetShooter.TryGetComponent<ITargetable>(out var targetable))
            {
                Debug.LogError("Target has no ITargetable");
                return;
            }

            aimTarget = targetable.GetTargetTransform();
            prevTargetPosition = aimTarget.position;

            // Hook aim constraints once
            var sources = new WeightedTransformArray() { new WeightedTransform(aimTarget, 1f) };
            foreach (var aimConstraint in aimConstraints)
            {
                aimConstraint.data.sourceObjects = sources;
            }

            // Force constraints rebuild
            var rb = GetComponent<RigBuilder>();
            rb.enabled = false; rb.enabled = true;

            Debug.Log("Target set to" + aimTarget.name);
        }

        public void DisableAgent()
        {
            SetAim(false);

            if (!agent.enabled) return;

            agent.isStopped = true;
            agent.updateRotation = true; // restore agent control when re-enabled
        }

        public void ReEnableAgent()
        {
            if(!agent.enabled) return;

            agent.isStopped = false;
            if (targetShooter != null)
            {
                TrySetDestination(GetTargetPosition());
            }
        }

        private void OnFootstep(AnimationEvent animationEvent) 
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                var FootstepAudioVolume = 1f;
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.position, FootstepAudioVolume);
                }
            }
        }
        private void OnLand(AnimationEvent animationEvent) { }

        // ==== Helpers ========================================================

        // Smooth aim-weight on the server; clients just read the netvar
        private void TickAimWeight()
        {
            float target = _isAiming ? 1f : 0f;
            constraintWeight.Value = Mathf.MoveTowards(constraintWeight.Value, target, 5f * Time.deltaTime);
        }

        // Only issue SetDestination if it’s meaningfully different and agent can path
        private void TrySetDestination(Vector3 dest)
        {
            if (!agent.isOnNavMesh) return;

            // Avoid spamming identical destinations
            if ((agent.destination - dest).sqrMagnitude < 0.25f && !agent.pathPending) return;

            // Don’t overwrite while computing a path
            if (agent.pathPending) return;

            if (NavMesh.SamplePosition(dest, out var hit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }

        // Manual rotate only if we’re misaligned enough; prevents micro-jitter, and aiming
        private void SmartFaceTarget(Vector3 targetPos)
        {
            Vector3 to = targetPos - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(to, Vector3.up);

            // only rotate if angle is significant
            float angle = Quaternion.Angle(transform.rotation, targetRot);
            if (angle > 1.5f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSlerpSpeed * Time.deltaTime);
            }
        }

        private static Vector3 GetTargetCenter(Transform root)
        {
            // Prefer collider center if present; fallback to transform position
            if (root.TryGetComponent<Collider>(out var col))
                return col.bounds.center;

            // Search children once (cheap)
            var childCol = root.GetComponentInChildren<Collider>();
            return childCol ? childCol.bounds.center : root.position;
        }
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (!IsServer) return;

            PlayerShootable.OnAnyPlayerDead -= PlayerShootable_OnAnyPlayerDead;
            GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
            GameMultiPlayer.Instance.OnPlayerDisconnected -= GameMultiplayer_OnPlayerDisconnected;
        }
    }
}
