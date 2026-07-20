using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CardBattle.Network
{
    public class GameNetwork : MonoBehaviour
    {
        public static GameNetwork Instance { get; private set; }

        [SerializeField] private ServerConfig serverConfig;

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Packet> _incoming = new();
        private readonly List<byte> _recvBuffer = new();
        private bool _connected;

        public bool IsConnected => _connected;
        public uint Uid { get; private set; }
        public string Token { get; private set; } = string.Empty;

        public event Action<LoginResult> OnLoginResult;
        public event Action<MatchResult> OnMatchResult;
        public event Action<BattleStateView> OnBattleStart;
        public event Action<BattleStateView> OnBattleState;
        public event Action<BattleEndResult> OnBattleEnd;
        public event Action<uint> OnHeartbeat;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        /// <summary>Latest battle snapshot (survives late UI subscribe).</summary>
        public BattleStateView LastBattleState { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (serverConfig == null)
            {
                serverConfig = Resources.Load<ServerConfig>("Network/ServerConfig");
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            while (_incoming.TryDequeue(out var packet))
            {
                HandlePacket(packet);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public async Task ConnectAsync()
        {
            if (_connected)
            {
                return;
            }

            if (serverConfig == null)
            {
                throw new InvalidOperationException("ServerConfig is not assigned");
            }

            _client = new TcpClient();
            await _client.ConnectAsync(serverConfig.host, serverConfig.port);
            _stream = _client.GetStream();
            _connected = true;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
            Debug.Log($"[Network] connected to {serverConfig.host}:{serverConfig.port}");
        }

        public void SendLogin(string username, string password)
        {
            var payload = PacketCodec.PackLoginReq(username, password);
            SendPacket(MessageIds.C2S_LoginReq, payload);
        }

        public void SendHeartbeat()
        {
            SendPacket(MessageIds.C2S_Heartbeat);
        }

        public void SendMatch()
        {
            SendPacket(MessageIds.C2S_MatchReq);
        }

        public void SendBattleReady()
        {
            SendPacket(MessageIds.C2S_BattleReady);
        }

        public void SendPlayCard(byte handIndex)
        {
            SendPacket(MessageIds.C2S_PlayCard, PacketCodec.PackPlayCard(handIndex));
        }

        public void SendEndTurn()
        {
            SendPacket(MessageIds.C2S_EndTurn);
        }

        public void Disconnect()
        {
            if (!_connected)
            {
                Uid = 0;
                return;
            }

            _connected = false;
            _cts?.Cancel();

            try { _stream?.Close(); } catch { /* ignore */ }
            try { _client?.Close(); } catch { /* ignore */ }

            _stream = null;
            _client = null;
            _recvBuffer.Clear();
            Uid = 0;
            Token = string.Empty;
            OnDisconnected?.Invoke("disconnected");
            Debug.Log("[Network] disconnected");
        }

        private void SendPacket(ushort msgId, byte[] payload = null)
        {
            if (!_connected || _stream == null)
            {
                OnError?.Invoke("not connected");
                return;
            }

            var data = PacketCodec.Pack(msgId, payload);
            _stream.Write(data, 0, data.Length);
            _stream.Flush();
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var temp = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && _connected)
                {
                    var n = await _stream.ReadAsync(temp, 0, temp.Length, token);
                    if (n <= 0)
                    {
                        break;
                    }

                    for (var i = 0; i < n; i++)
                    {
                        _recvBuffer.Add(temp[i]);
                    }

                    while (PacketCodec.TryReadPacket(_recvBuffer, out var packet))
                    {
                        _incoming.Enqueue(packet);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] receive error: {ex.Message}");
                _incoming.Enqueue(new Packet(MessageIds.S2C_Error, Array.Empty<byte>()));
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                if (_connected)
                {
                    _connected = false;
                    _incoming.Enqueue(new Packet(0, Array.Empty<byte>()));
                }
            }
        }

        private void HandlePacket(Packet packet)
        {
            if (!_connected && packet.MsgId == 0)
            {
                OnDisconnected?.Invoke("connection lost");
                return;
            }

            switch (packet.MsgId)
            {
                case MessageIds.S2C_LoginResp:
                    var result = PacketCodec.ParseLoginResp(packet.Payload);
                    if (result.Ok)
                    {
                        Uid = result.Uid;
                        Token = result.Token;
                    }
                    OnLoginResult?.Invoke(result);
                    break;

                case MessageIds.S2C_MatchResp:
                    OnMatchResult?.Invoke(PacketCodec.ParseMatchResp(packet.Payload));
                    break;

                case MessageIds.S2C_BattleStart:
                    LastBattleState = PacketCodec.ParseBattleState(packet.Payload);
                    OnBattleStart?.Invoke(LastBattleState);
                    break;

                case MessageIds.S2C_BattleState:
                    LastBattleState = PacketCodec.ParseBattleState(packet.Payload);
                    OnBattleState?.Invoke(LastBattleState);
                    break;

                case MessageIds.S2C_BattleEnd:
                    OnBattleEnd?.Invoke(PacketCodec.ParseBattleEnd(packet.Payload));
                    break;

                case MessageIds.S2C_Heartbeat:
                    if (packet.Payload.Length >= 4)
                    {
                        var serverTime = PacketCodec.ReadUInt32BE(packet.Payload, 0);
                        OnHeartbeat?.Invoke(serverTime);
                    }
                    break;

                case MessageIds.S2C_Error:
                    OnError?.Invoke("server error");
                    break;

                default:
                    Debug.LogWarning($"[Network] unknown msg_id={packet.MsgId}");
                    break;
            }
        }
    }
}
