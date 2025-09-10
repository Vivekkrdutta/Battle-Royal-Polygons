
using System;
using Unity.Collections;
using Unity.Netcode;

public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public int FireArmIndex;
    public int PrefabIndex;
    public ulong PlayerID;
    public int PositionIndex;
    public bool Ready;
    public FixedString128Bytes Name;

    public readonly bool Equals(PlayerData other)
    {
        return PlayerID == other.PlayerID;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerID);
        serializer.SerializeValue(ref PositionIndex);
        serializer.SerializeValue(ref Ready);
        serializer.SerializeValue(ref FireArmIndex);
        serializer.SerializeValue(ref PrefabIndex);
        serializer.SerializeValue(ref Name);
    }
}