using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Grenade : NetworkBehaviour
{
    [SerializeField] private ExplosionsSO explosionsSO;
    [SerializeField] private float waitTimerMax = 1f;
    [SerializeField] private Type grenadeType = Type.Normal;
    [SerializeField] private float damage = 800f;
    [SerializeField] private float forceAmount = 600;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float launchSpeed = 30f;
    [SerializeField] private AudioClip blastClip;

    [HideInInspector] public ulong LauncherId;
    public enum Type
    {
        Normal,
        Smoke,
        Poison,
    }

    private float waitTimer = 0f;
    private bool firstHit = true;

    private bool readyToBlast = false;

    private void Update()
    {
        if (!IsServer) return;

        if (readyToBlast)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimerMax) 
            {
                readyToBlast =false;
                BlastClientRpc(forceAmount, damage);
            }
        }
    }

    [ClientRpc(RequireOwnership = false)]
    private void BlastClientRpc(float forceAmount, float damageAmount)
    {
        AudioSource.PlayClipAtPoint(blastClip, transform.position, 2f);

        if(grenadeType == Type.Poison)
        {
            // just spawn the smoke/ cloud
            var smoke = Instantiate(explosionsSO.PoisonSmoke,transform.position,Quaternion.identity);
            smoke.GetComponent<ParticleEffects>().ShooterIfAny = Player.GetPlayerForPlayerData(new PlayerData() { PlayerID = LauncherId }).transform;
            if (IsServer) StartCoroutine(DestroySelf());
            return;
        }

        if(grenadeType == Type.Smoke)
        {
            Instantiate(explosionsSO.GrenadeSmoke,transform.position,Quaternion.identity);
            if(IsServer) StartCoroutine(DestroySelf());
            return;
        }

        // 1. Spawn explosion effect
        Instantiate(explosionsSO.HugeBlast, transform.position, Quaternion.identity);
        Debug.Log("Instantiating blast");

        if (!IsServer) return; // down all are for server only.

        // 2. Detect nearby shootables
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        var shooter = Player.GetPlayerForPlayerData(new PlayerData() { PlayerID = LauncherId }).transform;

        foreach (Collider hit in hits)
        {
            // Check if object implements IShootable
            if (hit.TryGetComponent<IShootable>(out var shootable))
            {
                // Use collider's closest point to blast center as hit position
                Vector3 hitPoint = hit.ClosestPoint(transform.position);

                // 3. Apply explosion
                shootable.AddExplosionForce(forceAmount, hitPoint, 1f, damageAmount,shooter);

                Debug.Log("Adding explosive force to " + hit.name);
            }
        }

        StartCoroutine(DestroySelf());
    }

    private IEnumerator DestroySelf()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        GetComponent<NetworkObject>().Despawn(destroy:true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !firstHit) return;

        firstHit = false;
        readyToBlast = true;
    }
    public float GetLaunchSpeed() => launchSpeed;
}