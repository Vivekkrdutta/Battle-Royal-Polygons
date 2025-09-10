using Unity.Netcode;
using UnityEngine;

public class ObstacleShootable : NetworkBehaviour, IShootable
{
    private Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
        {
            // the rigidbody stays only on the server
            Destroy(rb);
        }
    }
    public void AddExplosionForce(float forceAmout, Vector3 position, float radius, float damageAmount, Transform shooter = null)
    {
        AddExplsionForceServerRpc(forceAmout, position, radius);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddExplsionForceServerRpc(float forceAmout, Vector3 position, float radius)
    {
        rb.AddExplosionForce(forceAmout / rb.mass, position, radius);
    }

    public void AddNormalDamage(float damage,Transform shoooter)
    {
        AddNormalDamageServerRpc(damage);
    }
    [ServerRpc(RequireOwnership = false)]
    private void AddNormalDamageServerRpc(float damage)
    {
        rb.AddExplosionForce(damage * 5f, transform.position, 1f);
    }

    public void AddTinyAmountOfDamage(float damage, Transform shoooter)
    {
        // Nothing to do here.
    }

    public bool IsLivingThing() => false;
}
