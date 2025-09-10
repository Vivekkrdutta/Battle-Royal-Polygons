
using System.Collections.Generic;
using UnityEngine;

public class Tent : MonoBehaviour
{
    [Header("For instantiating Players and Agents")]
    [SerializeField] private List<Transform> instantiationsTransforms;

    private static readonly List<Tent> _tentsList = new();
    public List<Transform> GetInstantiaionsTransforms() => instantiationsTransforms;
    public static List<Tent> GetTentsList() => new(_tentsList);
    private void Awake()
    {
        _tentsList.Add(this);
    }
    private void OnDestroy()
    {
        _tentsList.Remove(this);
    }

    public static void RandomizeTentsList()
    {
        for(int i = 0; i < _tentsList.Count; i++)
        {
            var randInd = Random.Range(0, i);
            (_tentsList[i], _tentsList[randInd]) = (_tentsList[randInd], _tentsList[i]);
        }
    }
    public bool CheckSphere(Transform t, LayerMask checkMask)
    {
        var radius = 5f;
        return (Physics.CheckSphere(t.position, radius, checkMask));
    }
}