using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.Multiplayer.Samples.Utilities.ClientAuthority
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        // This tells Netcode that the Owner (the Client) 
        // has permission to sync their position to the Server.
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}