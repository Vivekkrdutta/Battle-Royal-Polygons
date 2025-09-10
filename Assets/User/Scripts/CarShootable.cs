using AI;
using Unity.Netcode;
using UnityEngine;

public class CarShootable : NetworkBehaviour, IShootable
{
    [SerializeField] private GameObject[] finePartsVisuals;
    /// <summary>
    /// The minimum damage that must be accumulated from damages caused by tiny damages to register as a damage
    /// </summary>
    [Tooltip("The minimum damage that must be accumulated from damages caused by tiny damages to register as a damage")]
    [SerializeField] private float minDamageThresholdForTinyDamages = 40f;
    /// <summary>
    /// This is where fire and smoke will start
    /// </summary>
    [Tooltip("This is where fire and smoke will start")]
    [SerializeField] private Transform hoodTransform;

    /// <summary>
    /// This is a ui script and can be used to simulate the health visuals.
    /// </summary>
    [Tooltip("This is a ui script and can be used to simulate the health visuals.")]
    [SerializeField] private BarUI healthBarUI;

    [SerializeField] 
    private ExplosionsSO explosionsSO;

    [SerializeField]
    [Range(100f, 1500f)] 
    private float maxHealth = 1500f;

    [SerializeField] private float carHitDamage = 200f;

    /// <summary>
    /// By how much the car loses its health every second, once heated.
    /// </summary>
    [Tooltip("By how much the car loses its health every second, once heated")]
    [SerializeField] private float decayAmount = 20f;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private AudioClip blastClip;

    /// <summary>
    /// Below this health, smoke will start
    /// </summary>
    private float smokeStartHealthThreshold = 1000f;

    /// <summary>
    /// Below this health, fire will start, and so will auto destruct after few seconds
    /// </summary>
    private float fireStartHealthThreshold = 500f;

    public DamagedVisuals damagedVisuals;

    /// <summary>
    /// False if the car is burnt completely.
    /// </summary>
    private bool isCarAlive = true;
    private bool smokeCaughtup = false;
    private bool fireCauthup = false;
    private float health = 0f;
    private float lastUpdatedHealth = 0f;
    private float accumulatedDamage = 0f;

    private GameObject smoke, fire;

    [System.Serializable]
    public class DamagedVisuals
    {
        public Mesh Mesh;
        public Material Material;
        public Mesh ColliderMesh;
    }

    private void Awake()
    {
        health = maxHealth;
        lastUpdatedHealth = health;
        healthBarUI.ChangeValue(health / maxHealth);

        smokeStartHealthThreshold = maxHealth * 9 / 10;
        fireStartHealthThreshold = maxHealth * 1 / 3;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    private void Update()
    {
        if (!IsServer || !isCarAlive) return;

        // the below code runs only on the server
        if (fireCauthup)
        {
            DropHealth(decayAmount * Time.deltaTime);
        }
    }

    /// <summary>
    /// Will be used to update the health on all clients, at every 50 hp drop
    /// </summary>
    /// <param name="health"></param>
    [ClientRpc(RequireOwnership = true)]
    private void UpdateHealthClientRpc(float health)
    {
        this.health = health;
        healthBarUI.ChangeValue(this.health / maxHealth);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddExplosionForceServerRpc(float forceAmout, Vector3 position, float radius, float damageAmount)
    {
        float forceAmountMultiplier = 2f;

        rb.AddExplosionForce(forceAmout * forceAmountMultiplier / rb.mass, position, radius);

        DropHealth(damageAmount);
    }

    private void CheckFireAndSmoke()
    {
        if (!IsServer) return;

        if (!fireCauthup && health <= fireStartHealthThreshold)
        {
            fireCauthup = true;
            StartFireClientRpc();
        }

        else if (!smokeCaughtup && health <= smokeStartHealthThreshold)
        {
            smokeCaughtup = true;
            StartSmokeClientRpc();
        }
    }

    /// <summary>
    /// Only the server is able to Invoke this method on all clients.
    /// </summary>
    [ClientRpc(RequireOwnership = true)]
    private void StartSmokeClientRpc()
    {
        smokeCaughtup = true;
        // Instantiate the smoke
        smoke = Instantiate(explosionsSO.LightSmoke,hoodTransform,false);
        smoke.transform.localPosition = Vector3.zero;
    }

    [ClientRpc(RequireOwnership = true)]
    private void StartFireClientRpc()
    {
        fireCauthup = true;
        fire = Instantiate(explosionsSO.StrongFire,hoodTransform,false);
        fire.transform.localPosition = Vector3.zero;
    }

    public virtual void AddExplosionForce(float forceAmout, Vector3 position, float radius, float damageAmount, Transform shooter = null)
    {
        if (!isCarAlive) return;
        AddExplosionForceServerRpc(forceAmout, position, radius, damageAmount);
    }

    private void DropHealth(float damage)
    {
        if (!IsServer || !isCarAlive) return;

        health -= damage;

        CheckFireAndSmoke();

        if (health < 0)
        {
            DeclareCarIsDead();
            return;
        }

        if(lastUpdatedHealth - health >= 50f)
        {
            lastUpdatedHealth = health;

            // update the health on all clients
            UpdateHealthClientRpc(health);
        }
    }

    protected virtual void DeclareCarIsDead()
    {
        if(!IsServer) return;

        DeclareCarIsDeadClientRpc();
    }

    [ClientRpc(RequireOwnership = true)]
    private void DeclareCarIsDeadClientRpc()
    {
        isCarAlive = false;

        AudioSource.PlayClipAtPoint(blastClip, transform.position, 1f * GameProperties.VolumeScale);

        Destroy(fire);
        Destroy(smoke);

        // play dead smoke
        smoke = Instantiate(explosionsSO.DarkSmoke,hoodTransform.position,Quaternion.identity);
        smoke.GetComponent<ParticleEffects>().SetLifeTime(10f);

        // Instantiate a blast
        Instantiate(explosionsSO.HugeBlast, transform.position, Quaternion.identity);

        foreach(var item in finePartsVisuals)
        {
            Destroy(item);
        }

        Debug.Log("Car is dead");
        // Change the mesh filter and collider
        GetComponent<MeshFilter>().mesh = damagedVisuals.Mesh;
        GetComponent<MeshRenderer>().material = damagedVisuals.Material;
        GetComponent<MeshCollider>().sharedMesh = damagedVisuals.ColliderMesh;

        healthBarUI.ChangeValue(0f);
    }

    public void AddNormalDamage(float damage, Transform shooter = null)
    {
        if (!isCarAlive) return;
        DropHealth(damage);
    }

    public void AddTinyAmountOfDamage(float amount, Transform shooter = null)
    {
        if (!IsServer) return;

        accumulatedDamage += amount;
        if(accumulatedDamage >= minDamageThresholdForTinyDamages)
        {
            AddNormalDamage(accumulatedDamage);
            accumulatedDamage = 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(!IsServer) return;

        if(collision.gameObject.TryGetComponent(out IShootable shootable))
        {
            if (shootable is not PlayerShootable || shootable is not SmartAgentShootable) return;

            float vel = rb.linearVelocity.magnitude;
            float maxDamageMagnitude = 50f;

            float force = vel / maxDamageMagnitude * 100f;
            shootable.AddExplosionForce(force, collision.contacts[0].point, 3f, carHitDamage);
        }
    }

    protected GameObject[] GetFineObjects() => finePartsVisuals;
}
