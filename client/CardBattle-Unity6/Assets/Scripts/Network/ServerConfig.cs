using UnityEngine;

namespace CardBattle.Network
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "CardBattle/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        [Header("C++ Auth Service (register / login)")]
        public string authHost = "127.0.0.1";
        public int authPort = 8889;

        [Header("Skynet Gate (match / battle)")]
        public string host = "127.0.0.1";
        public int port = 8888;
    }
}
