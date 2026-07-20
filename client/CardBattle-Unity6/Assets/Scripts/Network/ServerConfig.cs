using UnityEngine;

namespace CardBattle.Network
{
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "CardBattle/Server Config")]
    public class ServerConfig : ScriptableObject
    {
        [Header("Skynet Gate")]
        public string host = "127.0.0.1";
        public int port = 8888;
    }
}
