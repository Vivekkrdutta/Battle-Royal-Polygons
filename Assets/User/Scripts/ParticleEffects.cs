using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ParticleEffects : MonoBehaviour
{
    [SerializeField] private float lifeTime = -1f;
    [SerializeField] private float damageRadius = 1.5f;
    /// <summary>
    /// How much damage each particle causes.
    /// </summary>
    [Tooltip("How much damage each particle causes.")]
    [SerializeField] private float damageAmount;
    [SerializeField] private float damageInterval = 0.2f; // apply damage every 0.2s
    private float elapsedTime = 0f;
    private float damageTimer = 0f;

    [HideInInspector]
    public Transform ShooterIfAny;

    private void Update()
    {
        if (lifeTime == -1f) return;

        elapsedTime += Time.deltaTime;

        if(elapsedTime > lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // Only for the server
        if (!NetworkManager.Singleton.IsServer) return;

        AddRegularTinyDamage();
    }

    public void SetLifeTime(float value)
    {
        lifeTime = value;
    }

    private void AddRegularTinyDamage()
    {
        if (!NetworkManager.Singleton.IsServer || damageAmount == 0f) return;

        damageTimer += Time.deltaTime;
        if(damageTimer >= damageInterval)
        {
            damageTimer = 0f;
            Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out IShootable shootable))
                {
                    shootable.AddTinyAmountOfDamage(damageAmount,ShooterIfAny);
                }
            }
        }
    }
}
