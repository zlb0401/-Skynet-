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
        private bool _disconnectNotified;
        private int _connectionEpoch;
        private const ushort MsgDisconnect = 0;

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
            _disconnectNotified = false;
            _cts = new CancellationTokenSource();
            var epoch = ++_connectionEpoch;
            var token = _cts.Token;
            _ = Task.Run(() => ReceiveLoop(epoch, token));
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
            // Always clear session fields so UI can treat us as logged out.
            Uid = 0;
            Token = string.Empty;
            _recvBuffer.Clear();

            if (!_connected && _client == null && _stream == null)
            {
                return;
            }

            _connected = false;
            // Invalidate any in-flight receive loop so its finally cannot clear a newer session.
            _connectionEpoch++;

            var cts = _cts;
            _cts = null;
            var stream = _stream;
            _stream = null;
            var client = _client;
            _client = null;

            try
            {
                cts?.Cancel();
            }
            catch
            {
                /* ignore */
            }

            // Closing TcpClient/NetworkStream on the Unity main thread while ReceiveLoop
            // is blocked in ReadAsync can deadlock in standalone Mono builds (Editor often OK).
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { stream?.Close(); } catch { /* ignore */ }
                try { client?.Close(); } catch { /* ignore */ }
                try { cts?.Dispose(); } catch { /* ignore */ }
            });

            // Raise disconnect on the next Update (main thread), never from the worker.
            _incoming.Enqueue(new Packet(MsgDisconnect, Array.Empty<byte>()));
            Debug.Log("[Network] disconnect requested");
        }

        private void SendPacket(ushort msgId, byte[] payload = null)
        {
            if (!_connected || _stream == null)
            {
                OnError?.Invoke("not connected");
                return;
            }

            try
            {
                var data = PacketCodec.Pack(msgId, payload);
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Network] send failed: {ex.Message}");
                _connected = false;
                _incoming.Enqueue(new Packet(MsgDisconnect, Array.Empty<byte>()));
            }
        }

        private async Task ReceiveLoop(int epoch, CancellationToken token)
        {
            var temp = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && _connected && _stream != null && epoch == _connectionEpoch)
                {
                    var stream = _stream;
                    if (stream == null)
                    {
                        break;
                    }

                    var n = await stream.ReadAsync(temp, 0, temp.Length, token).ConfigureAwait(false);
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
            catch (ObjectDisposedException)
            {
                // stream closed during logout
            }
            catch (Exception ex)
            {
                // Do not invoke Unity events from this worker thread.
                Debug.LogError($"[Network] receive error: {ex.Message}");
                if (epoch == _connectionEpoch)
                {
                    _incoming.Enqueue(new Packet(MessageIds.S2C_Error, Array.Empty<byte>()));
                }
            }
            finally
            {
                if (epoch == _connectionEpoch)
                {
                    _connected = false;
                    _incoming.Enqueue(new Packet(MsgDisconnect, Array.Empty<byte>()));
                }
            }
        }

        private void NotifyDisconnected(string msg)
        {
            if (_disconnectNotified)
            {
                return;
            }

            _disconnectNotified = true;
            OnDisconnected?.Invoke(msg);
        }

        private void HandlePacket(Packet packet)
        {
            if (packet.MsgId == MsgDisconnect)
            {
                // Stale disconnect from a previous socket — ignore if already reconnected.
                if (_connected)
                {
                    return;
                }

                NotifyDisconnected("disconnected");
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
