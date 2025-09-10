
using StarterAssets;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class PlayerShootable : NetworkBehaviour, IShootable 
{
    /// <summary>
    /// To ThirdPersonShooter.
    /// </summary>
    public event EventHandler OnPlayerOwnerDead;
    /// <summary>
    /// The method will invoke on all clients, at the time the player is dead.
    /// </summary>
    public static event EventHandler<OnAnyPlayerDeadEventArgs> OnAnyPlayerDead;
    /// <summary>
    /// For score updates. Invoked on all clients.
    /// </summary>
    public static event EventHandler OnAnyPlayerSpawned;

    [SerializeField] private float maxHealth;
    [SerializeField] private float minDamageThresholdFromTinyDamages = 30f;
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private BarUI healthBarUI;
    [SerializeField] private ParticleSystem bloodSpats;
    [SerializeField] private ParticleSystem deathVFX;
    [SerializeField] private float mass = 1f;
    [SerializeField] private Rig rig;
    [SerializeField] private Transform rootTransform;
    [SerializeField] private float healPercent = 10f;
    [SerializeField] private float healStartTimerMaxAfterDamage = 7f;
    [SerializeField] private float waitTimeWhileHealingMax = 0.5f;

    private Vector3 explosionVelocity;
    private Volume globalVolume;

    private readonly float drag = 5f;   // controls how quickly force decays
    private bool isExploding = false;
    private bool isAlive = true;
    private bool isHealing = false;
    private readonly NetworkVariable<float> health = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float accumulatedDamage = 0f;
    private float timePassedWithoutHealing = 0f;
    private float waitTimerWhileHealing = 0f;

    public class OnAnyPlayerDeadEventArgs : EventArgs
    {
        public Player Player;
    }
    private void HealthValueChanged(float previousValue, float newValue)
    {
        if (IsServer)
        {
            // reset the ehealing factor
            timePassedWithoutHealing = 0;
        }

        Status.Instance.SetHealth(newValue / maxHealth);

        if (IsOwner)
        {
            float val = -100f + 200 * newValue / maxHealth;

            // show the black/white style
            if(globalVolume.profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments.saturation.value = val;
            }
        }
        if (IsOwner && newValue < previousValue) // that only if it decreased.
        {
            NetCamera.PlayerDamageEffect();
            return;
        }

        healthBarUI.ChangeValue(newValue / maxHealth);
    }

    private void Awake()
    {
        GameManager.OnMatchEnded += GameManager_OnMatchEnded;
    }

    private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
    {
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
        GetComponent<PlayerInput>().enabled = false;
        GetComponent<StarterAssetsInputs>().enabled = false;

        isAlive = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SetupName();

        globalVolume = GameManager.Instance.globalVolume;

        health.OnValueChanged += HealthValueChanged;

        if (IsServer)
        {
            IEnumerator SetHealth()
            {
                yield return new WaitForSecondsRealtime(0.5f);
                health.Value = maxHealth;
            }

            StartCoroutine(SetHealth());
        }
        OnAnyPlayerSpawned?.Invoke(this, null);
    }

    private void SetupName()
    {
        var name = GameMultiPlayer.Instance.GetSelfPlayerData().Name;
        playerName.text = name.ToString();
    }
    public void AddExplosionForce(float forceAmout, Vector3 position, float radius, float damageAmount, Transform shooter)
    {
        Debug.Log("Player " + name + " Was hit");

        var dir = (transform.position - position).normalized;

        Vector3 force = forceAmout * dir.normalized;

        AddExplosionForceServerRpc(force,damageAmount,OwnerClientId,shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddExplosionForceServerRpc(Vector3 force,float damage, ulong selfClientId,ulong shooterId)
    {        
        DropHealth(damage,shooterId);

        AddExplosionForceClientRpc(force, new ClientRpcParams()
        {
            Send = new ClientRpcSendParams()
            {
                TargetClientIds = new List<ulong>() { selfClientId },
            },
        });
    }

    [ClientRpc(RequireOwnership = false)]
    private void AddExplosionForceClientRpc(Vector3 force, ClientRpcParams clientRpcParams = default)
    {
        // only the owner will be invoked
        // add explosion force locally

        if (!IsOwner) return;

        explosionVelocity = force / mass;
        explosionVelocity.y = Mathf.Min(0.2f, Mathf.Abs(explosionVelocity.y));
        isExploding = true;
    }

    private void Update()
    {
        if (IsServer && isAlive) HandleHealingLogic();

        if (!IsOwner || !isExploding) return;

        transform.position += explosionVelocity * Time.deltaTime;

        explosionVelocity = Vector3.Lerp(explosionVelocity,Vector3.zero, drag *  Time.deltaTime);

        // snap
        if(explosionVelocity.sqrMagnitude <= 0.01f)
        {
            explosionVelocity = Vector3.zero;
            isExploding = false;
        }

        if (!isAlive)
        {
            if (!Physics.CheckSphere(rootTransform.position, 0.1f))
            {
                float fallSpeed = 5f;
                transform.position += fallSpeed * Time.deltaTime * Vector3.down;
                return;
            }
            Debug.LogWarning("Fallen on ground now");
        }
    }

    private void HandleHealingLogic()
    {
        timePassedWithoutHealing += Time.deltaTime;
        if (health.Value < maxHealth && timePassedWithoutHealing >= healStartTimerMaxAfterDamage)
        {
            isHealing = true;
        }

        if (!isHealing) return;

        waitTimerWhileHealing += Time.deltaTime;

        if (waitTimerWhileHealing < waitTimeWhileHealingMax) return;

        waitTimerWhileHealing = 0f;
        Heal(healPercent / 100 * maxHealth);
        if (health.Value >= maxHealth)
        {
            isHealing = false;
        }
    }

    private void Heal(float value)
    {
        if(!IsServer) return;

        health.Value = Mathf.Clamp(health.Value + value, 0f, maxHealth);
    }
    public void AddTinyAmountOfDamage(float damage, Transform shooter = null)
    {
        // ensure its the server
        if (!IsServer) return;
        // accumulate the damage
        accumulatedDamage += damage;
        if(accumulatedDamage >= minDamageThresholdFromTinyDamages)
        {
            AddNormalDamageServerRpc(accumulatedDamage, shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue); // this damage is done by the server
            accumulatedDamage = 0f;
        }
    }
    public void AddNormalDamage(float damage, Transform shooter = null)
    {
        AddNormalDamageServerRpc(damage,shooter ? shooter.GetComponent<ThirdPersonShooter>().OwnerClientId : ulong.MaxValue);
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddNormalDamageServerRpc(float damage,ulong shooterId, bool induceBlood = true)
    {
        // just do damage
        DropHealth(damage,shooterId);

        if (induceBlood) ShowBloodClientRpc();
    }

    [ClientRpc(RequireOwnership = false)]
    private void ShowBloodClientRpc()
    {
        bloodSpats.Play();
    }
    
    // Server only
    private void DropHealth(float damage,ulong shoooterId)
    {
        if(!IsServer || !isAlive) return;

        health.Value = Mathf.Clamp(health.Value - damage, 0f, maxHealth);

        isHealing = false;
        timePassedWithoutHealing = 0f;
        waitTimerWhileHealing = 0f;

        if (health.Value <= 0 && isAlive)
        {
            isAlive = false;
            Debug.LogWarning("Player " + OwnerClientId + " is dead");
            health.Value = 0;
            healthBarUI.ChangeValue(0f);

            // register the death first. then inform all the clients.
            GameManager.Instance.RegisterDeath(shoooterId,OwnerClientId);
            PlayDead();

            return;
        }

        return;
    }

    private void PlayDead()
    {
        if (!IsServer) return;

        PlayDeadClientRpc();
    }

    [ClientRpc(RequireOwnership = false)]
    private void PlayDeadClientRpc()
    {
        // on player dead is for all.
        OnPlayerOwnerDead?.Invoke(this, null);

        // inform game manager of all the clients & server
        OnAnyPlayerDead?.Invoke(this, new OnAnyPlayerDeadEventArgs
        {
            Player = GetComponent<Player>(),
        });

        var controller = GetComponent<CharacterController>();

        GetComponent<ThirdPersonController>().enabled = false;
        controller.enabled = false;

        var animator = GetComponent<Animator>();

        animator.applyRootMotion = true;

        int normalAimLayer = 0;
        animator.SetLayerWeight(normalAimLayer, 1);

        int rand = Random.Range(0, 3);

        // play one of three death animations
        animator.SetTrigger("Death" + rand);

        Instantiate(deathVFX.gameObject, rootTransform.position, Quaternion.identity);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
    }
}
