
using System;
using Unity.Netcode;
/// <summary>
/// Used as an alternative to the Existing PlayerData due to it being less Data bulky.
/// </summary>
public struct PlayerGamePlayData : INetworkSerializable,IEquatable<PlayerGamePlayData>
{
    public ulong PlayerId;
    public int KillsCount;
    public int DeathsCount;

    public bool Equals(PlayerGamePlayData other)
    {
        return PlayerId == other.PlayerId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref KillsCount);
        serializer.SerializeValue(ref DeathsCount);
    }
}