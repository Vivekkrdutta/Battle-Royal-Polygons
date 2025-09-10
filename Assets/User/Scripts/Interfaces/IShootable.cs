using System;
using UnityEngine;
public interface IShootable
{
    void AddExplosionForce(float forceAmout, Vector3 position, float radius, float damageAmount,Transform shooter = null);
    void AddNormalDamage(float damage,Transform shooter = null);
    /// <summary>
    /// Shold only be run on the Server.
    /// </summary>
    /// <param name="damage"></param>
    void AddTinyAmountOfDamage(float damage, Transform shooter = null);
}
