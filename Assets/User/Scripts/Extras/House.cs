using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attatched to selected Apartments. Aids in players and Agents instantiations.
/// </summary>
public class House : MonoBehaviour
{
    [Header("For instantiating Players and Agents")]
    [SerializeField] private List<Transform> instantiationsTransforms;

    private static readonly List<House> _housesList = new();
    public List<Transform> GetInstantiaionsTransforms() => instantiationsTransforms;
    public static List<House> GetHousesList() => new (_housesList);
    private void Awake()
    {
        _housesList.Add(this);
    }
    private void OnDestroy()
    {
        _housesList.Remove(this);
    }
    public bool CheckSphere(Transform t,LayerMask checkMask)
    {
        var radius = 5f;
        return (Physics.CheckSphere(t.position, radius, checkMask));
    }
}
