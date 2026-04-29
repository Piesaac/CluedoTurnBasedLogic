using UnityEngine;
using Unity.Netcode;

namespace CardList
{   
    // Creates a structure for the cards and their information, e.g. their category and int value within the category enum
    [System.Serializable]
    public struct Card : INetworkSerializable
    {
        public enum CardType { Suspect, Weapon, Room }
        public CardType type;
        public int value;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Allows the type and value of cards to be communicated across the server
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref value);
        }
    }
    
    // Defines the suspects
    public enum Who
    {
        Mustard,
        Plum,
        Green,
        Peacock,
        Scarlett,
        Orchid
    }

    // Defines the weapons
    public enum What
    {
        Dagger,
        Candlestick,
        Revolver,
        Rope,
        Pipe,
        Spanner
    }

    // Defines the locations
    public enum Where
    {
        Hall,
        Lounge,
        Dining,
        Kitchen,
        Ballroom,
        Conservatory,
        Billiard,
        Library,
        Study
    }
}
