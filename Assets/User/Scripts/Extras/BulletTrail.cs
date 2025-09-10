using UnityEngine;

public class BulletTrail : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private float destroyDistance = 0.1f;

    private Vector3 target;
    private bool goNow = false;
    public void SetTarget(Vector3 target)
    {
        this.target = target;
        goNow = true;
    }

    private void Update()
    {
        if (!goNow) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            speed * Time.deltaTime);

        if(Vector3.Distance(transform.position, target) <= destroyDistance)
        {
            goNow = false;
            Destroy(gameObject);
        }
    }

}
