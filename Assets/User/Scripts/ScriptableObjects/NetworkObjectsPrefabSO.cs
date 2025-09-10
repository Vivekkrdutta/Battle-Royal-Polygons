
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "NetworkObjectsPrefabSO", menuName = "Scriptable Objects/NetworkObjectsPrefabSO")]

public class NetworkObjectsPrefabSO : ScriptableObject
{
    public NetworkObject[] List;
}