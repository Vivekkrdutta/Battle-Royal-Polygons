using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;


namespace AI
{
    public class SmartAgentShootable : NetworkBehaviour, IShootable
    {
        // to GameManager
        public static event EventHandler<OnAnyAgentDeadEventArgs> OnAnyAgentDead;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float minDamageThresholdFromTinyDamages = 30f;
        [SerializeField] private BarUI healthBarUI;
        [SerializeField] private ParticleSystem bloodSpats;
        [SerializeField] private ParticleSystem deathVFX;
        [SerializeField] private float mass = 1f;

        [Header("Explosion Force")]
        [SerializeField] private float drag = 5f;   // controls how quickly force decays

        private Vector3 explosionVelocity;
        private bool isExploding = false;
        private bool alive = true;
        private float accumulatedDamage = 0f;
        private readonly NetworkVariable<float> health =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Animator animator;     // reserved for death anim if needed
        private SmartAgentBehaviour agentBehaviour;

        public class OnAnyAgentDeadEventArgs : EventArgs
        {
            public SmartAgentShootable AgentShootable;
        }

        // ---------------- LIFECYCLE ----------------
        private void Awake()
        {
            animator = GetComponent<Animator>();
            agentBehaviour = GetComponent<SmartAgentBehaviour>();

            GameManager.OnMatchEnded += GameManager_OnMatchEnded;
        }

        private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
        {
            GameManager.OnMatchEnded -= GameManager_OnMatchEnded;

            alive = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            health.OnValueChanged += HealthValueChanged;

            if (IsServer)
            {
                health.Value = maxHealth;
            }

            // sync UI instantly on clients when they spawn
            HealthValueChanged(0f, health.Value);
        }

        public void SetMaxHealth(float maxHealth)
        {
            this.maxHealth = maxHealth;
            health.Value = maxHealth;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            health.OnValueChanged -= HealthValueChanged;
            GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
            Debug.Log("Agent is destroying : " + name);
        }

        // ---------------- HEALTH ----------------
        private void HealthValueChanged(float previousValue, float newValue)
        {
            healthBarUI.ChangeValue(Mathf.Clamp01(newValue / maxHealth));
        }

        private void DropHealth(float damage,ulong killerId)
        {
            if (!IsServer) return;

            health.Value = Mathf.Max(0, health.Value - damage);

            if (health.Value <= 0 && alive)
            {
                alive = false;
                Debug.LogWarning($"Agent {name} (Owner {OwnerClientId}) is dead");

                GameManager.Instance.RegisterDeath(killerId, ulong.MaxValue); // this does not have any player id

                // TODO: trigger death sequence (animator, ragdoll, despawn, etc.)
                PlayDead();
            }
        }

        private void PlayDead()
        {
            if (!IsServer) return;
           
            PlaydeadClientRpc();
            // trigger the event for gamemanager
            OnAnyAgentDead?.Invoke(this, new OnAnyAgentDeadEventArgs
            {
                AgentShootable = this,
            });
        }

        [ClientRpc(RequireOwnership = true)]
        private void PlaydeadClientRpc()
        {
            animator.applyRootMotion = true;
            // disable the agent shootable's functionality at local levels
            GetComponent<SmartAgentBehaviour>().SetDead();
            int rand = UnityEngine.Random.Range(0, 3);

            // play dead animation.( one of three )
            animator.SetTrigger("Death" + rand);

            Instantiate(deathVFX.gameObject,transform.position, Quaternion.identity);
        }

        public void AddTinyAmountOfDamage(float damage, Transform shooter = null)
        {
            if (!IsServer) return;

            accumulatedDamage += damage;
            if (accumulatedDamage >= minDamageThresholdFromTinyDamages)
            {
                DropHealth(accumulatedDamage, shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue);
                accumulatedDamage = 0f;
            }
        }

        public void AddNormalDamage(float damage, Transform shooter = null)
        {
            AddNormalDamageServerRpc(damage,shooter, shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue);
        }

        [ServerRpc(RequireOwnership = false)]
        private void AddNormalDamageServerRpc(float damage,bool showblood = true,ulong killerId = ulong.MaxValue)
        {
            DropHealth(damage,killerId);

            if (showblood) ShowBloodClientRpc();
        }

        [ClientRpc(RequireOwnership = false)]
        private void ShowBloodClientRpc()
        {
            bloodSpats.Play();
        }

        // ---------------- EXPLOSION ----------------
        public void AddExplosionForce(float forceAmount, Vector3 position, float radius, float damageAmount, Transform shooter = null)
        {
            // Always tell server
            var dir = (transform.position - position).normalized;
            if (dir == Vector3.zero) dir = Vector3.forward; // safe fallback

            Vector3 force = dir * forceAmount / mass;

            AddExplosionForceServerRpc(force, damageAmount,shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue);
        }

        [ServerRpc(RequireOwnership = false)]
        private void AddExplosionForceServerRpc(Vector3 force, float damage,ulong killerId = ulong.MaxValue)
        {
            explosionVelocity = force;
            explosionVelocity.y = Mathf.Min(0.2f, Mathf.Abs(explosionVelocity.y));
            isExploding = true;

            agentBehaviour.DisableAgent();
            DropHealth(damage,killerId);
        }

        private void Update()
        {
            if (!IsServer) return;

            if (isExploding)
            {
                transform.position += explosionVelocity * Time.deltaTime;
                explosionVelocity = Vector3.Lerp(explosionVelocity, Vector3.zero, drag * Time.deltaTime);

                if (explosionVelocity.sqrMagnitude <= 0.01f)
                {
                    explosionVelocity = Vector3.zero;
                    isExploding = false;
                    agentBehaviour.ReEnableAgent();
                }
            }
        }
    }
}

