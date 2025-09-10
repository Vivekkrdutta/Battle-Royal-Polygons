
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BarrelShootable : NetworkBehaviour, IShootable
{
    [SerializeField] private AudioClip blastSound;
    [SerializeField] private GameObject blast;
    [SerializeField] private float damageAmount = 1000f;
    [SerializeField] private float forceAmount = 500f;
    [SerializeField] private float radius = 5f;
    private bool notblastedyet = true;
    public void AddExplosionForce(float forceAmout, Vector3 position, float radius, float damageAmount, Transform shooter = null)
    {
        BlastServerRpc();
    }

    public void AddNormalDamage(float damage, Transform shooter = null)
    {
        BlastServerRpc();
    }

    public void AddTinyAmountOfDamage(float damage, Transform shooter = null)
    {
        BlastServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void BlastServerRpc()
    {
        if (!notblastedyet) return;

        notblastedyet = false;
        BlastClientRpc();

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach(var hit in hits)
        {
            if (hit.TryGetComponent(out IShootable shootable))
            {
                shootable.AddExplosionForce(forceAmount, hit.ClosestPoint(transform.position), 2.5f, damageAmount);
            }
        }

        StartCoroutine(DestroySelf());
    }

    private IEnumerator DestroySelf()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        GetComponent<NetworkObject>().Despawn(destroy: true);
    }

    [ClientRpc(RequireOwnership = false)]
    private void BlastClientRpc()
    {
        AudioSource.PlayClipAtPoint(blastSound, transform.position, 1f * GameProperties.VolumeScale);
        Instantiate(blast, transform.position, Quaternion.identity) ;
        Destroy(gameObject);
    }
}