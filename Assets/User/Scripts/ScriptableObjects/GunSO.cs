using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GunSO", menuName = "Scriptable Objects/GunSO")]
public class GunSO : ScriptableObject
{
    /// <summary>
    /// Contains the orientation information of itself inside the Gunholder. Apply them
    /// </summary>
    [Header("Orientations Details")]
    public Orientation Self;
    /// <summary>
    /// For animation Rigging : LeftHand, Two bone IK
    /// </summary>
    public Orientation LeftHandPlace;
    /// <summary>
    /// Will be used for Explosive nature
    /// </summary>
    public Type GunType;

    [Serializable]
    public struct Orientation
    {
        public Vector3 LocalPosition;
        public Vector3 LocalRotation;
        public Vector3 LocalScale;
    }
    [Serializable]
    public enum Type
    {
        AssultRifle,
        ShotGun,
        MachineGun,
        GrenadeLauncher,
    }

    [Header("Ras Stats of the Gun")]
    public int ExposivePower;
    public int DamageAmount;
    public float RateOfFire;
    public float Range;
    public float Recoil;
    public float Spread;
    public float ReloadTime;

    [Range(0f,1f)] 
    public float Sensitivity;

    public int AmmoCapacity;

    /// <summary>
    /// For UI in the mainmenuscene
    /// </summary>
    [Header("UI Visuals")]
    public Sprite GunVisualSprite;

    public Sprite GunVisualSpriteStraight;
    public Sprite CursorSprite;
    /// <summary>
    /// For UI only
    /// </summary>
    public Sprite AmmoVisualSprite;
    public string Name;
    public string FullName;
    public string DescriptionOneLine;

    [Header("Sounds, in order")]
    public AudioClip[] FireSounds;
}
