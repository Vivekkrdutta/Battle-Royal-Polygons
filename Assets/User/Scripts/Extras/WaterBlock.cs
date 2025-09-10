using UnityEngine;

public class WaterBlock : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out IShootable shootable))
        {
            GameMultiPlayer.Instance.NetworkDestroy(other.transform);
        }
    }
}
