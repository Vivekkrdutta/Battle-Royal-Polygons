using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GunsListSO", menuName = "Scriptable Objects/GunsListSO")]
public class GunsListSO : ScriptableObject
{
    public List<FireArm> FireArmsList;
}