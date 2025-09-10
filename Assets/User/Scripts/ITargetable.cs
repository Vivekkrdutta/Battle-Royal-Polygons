
using UnityEngine;
/// <summary>
/// Gives the property to be targeted by Agents, enemies etc.
/// </summary>
public interface ITargetable
{
    Transform GetTargetTransform();
}