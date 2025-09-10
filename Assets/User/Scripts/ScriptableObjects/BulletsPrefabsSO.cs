using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BulletsPrefabsSO", menuName = "Scriptable Objects/BulletsPrefabsSO")]
public class BulletsPrefabsSO : ScriptableObject
{
    public List<BulletTrail> BulletTrailPrefabsList;
}
