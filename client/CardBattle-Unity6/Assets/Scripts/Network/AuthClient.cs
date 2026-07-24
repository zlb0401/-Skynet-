using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CardBattle.Network
{
    /// <summary>
    /// Talks to the C++ Auth service (default TCP 8889) for captcha / register / login / inventory.
    /// After auth success, connect to Skynet gate and call <see cref="GameNetwork.SendTokenLogin"/>.
    /// </summary>
    public static class AuthClient
    {
        public static async Task<LoginResult> LoginAsync(string host, int port, string username, string password)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_LoginReq, PacketCodec.PackLoginReq(username, password));
            return await ReadLoginAsync(stream, MessageIds.S2C_LoginResp);
        }

        public static async Task<CaptchaChallenge> FetchCaptchaAsync(string host, int port)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_CaptchaReq, Array.Empty<byte>());
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_CaptchaResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseCaptchaResp(pkt.Value.Payload)
                : new CaptchaChallenge(string.Empty, string.Empty);
        }

        public static async Task<LoginResult> RegisterAsync(
            string host,
            int port,
            string username,
            string password,
            string captchaId,
            string captchaAnswer)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            var payload = PacketCodec.PackRegisterReq(username, password, captchaId, captchaAnswer);
            await WritePacketAsync(stream, MessageIds.C2S_RegisterReq, payload);
            return await ReadLoginAsync(stream, MessageIds.S2C_RegisterResp);
        }

        public static async Task<InventorySnapshot> FetchInventoryAsync(string host, int port, string token)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_InventoryReq, PacketCodec.PackStr8(token));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_InventoryResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseInventoryResp(pkt.Value.Payload)
                : new InventorySnapshot(false, "inventory timeout", 0, 0, 0, 0, null);
        }

        public static async Task<ClaimRewardResult> ClaimRewardAsync(
            string host, int port, string token, string stageKey, string runId)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            var payload = PacketCodec.PackClaimRewardReq(token, stageKey, runId);
            await WritePacketAsync(stream, MessageIds.C2S_ClaimRewardReq, payload);
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_ClaimRewardResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseClaimRewardResp(pkt.Value.Payload)
                : new ClaimRewardResult(false, "claim timeout", 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public static async Task<ListUpgradesResult> ListUpgradesAsync(string host, int port, string token)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_ListUpgradesReq, PacketCodec.PackStr8(token));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_ListUpgradesResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseListUpgradesResp(pkt.Value.Payload)
                : new ListUpgradesResult(false, "list upgrades timeout", 0, 0, 0, 0, null);
        }

        public static async Task<UpgradeCardResult> UpgradeCardAsync(string host, int port, string token, string cardKey)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_UpgradeCardReq, PacketCodec.PackUpgradeCardReq(token, cardKey));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_UpgradeCardResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseUpgradeCardResp(pkt.Value.Payload)
                : new UpgradeCardResult(false, "upgrade timeout", string.Empty, 0, 0, 0, 0, 0, 0);
        }

        public static async Task<UseItemResult> UseItemAsync(string host, int port, string token, ushort itemId)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_UseItemReq, PacketCodec.PackUseItemReq(token, itemId));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_UseItemResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseUseItemResp(pkt.Value.Payload)
                : new UseItemResult(false, "use item timeout", 0, 0, 0, 0, string.Empty, null);
        }

        public static async Task<DeckSnapshot> GetDeckAsync(string host, int port, string token)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_GetDeckReq, PacketCodec.PackStr8(token));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_GetDeckResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseDeckResp(pkt.Value.Payload)
                : new DeckSnapshot(false, "get deck timeout", null, null);
        }

        public static async Task<DeckSnapshot> SaveDeckAsync(string host, int port, string token, string[] cards)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_SaveDeckReq, PacketCodec.PackSaveDeckReq(token, cards));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_SaveDeckResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseDeckResp(pkt.Value.Payload)
                : new DeckSnapshot(false, "save deck timeout", null, null);
        }

        public static async Task<GachaResult> GachaAsync(string host, int port, string token, byte payType, byte count = 1)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            using var stream = client.GetStream();
            await WritePacketAsync(stream, MessageIds.C2S_GachaReq, PacketCodec.PackGachaReq(token, payType, count));
            var pkt = await ReadPacketAsync(stream, MessageIds.S2C_GachaResp, 8);
            return pkt.HasValue
                ? PacketCodec.ParseGachaResp(pkt.Value.Payload)
                : new GachaResult(false, "gacha timeout", 0, 0, 0, 0, 0, null);
        }

        private static async Task WritePacketAsync(NetworkStream stream, ushort msgId, byte[] payload)
        {
            var packet = PacketCodec.Pack(msgId, payload);
            await stream.WriteAsync(packet, 0, packet.Length);
        }

        private static async Task<LoginResult> ReadLoginAsync(NetworkStream stream, ushort respId)
        {
            var pkt = await ReadPacketAsync(stream, respId, 8);
            return pkt.HasValue
                ? PacketCodec.ParseLoginResp(pkt.Value.Payload)
                : new LoginResult(false, "auth timeout", 0, string.Empty);
        }

        private static async Task<Packet?> ReadPacketAsync(NetworkStream stream, ushort expectId, int timeoutSec)
        {
            var buffer = new List<byte>();
            var temp = new byte[4096];
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);

            while (DateTime.UtcNow < deadline)
            {
                if (!stream.DataAvailable && buffer.Count < 2)
                {
                    await Task.Delay(20);
                    continue;
                }

                if (stream.DataAvailable)
                {
                    var n = await stream.ReadAsync(temp, 0, temp.Length);
                    if (n <= 0)
                    {
                        break;
                    }

                    for (var i = 0; i < n; i++)
                    {
                        buffer.Add(temp[i]);
                    }
                }

                while (PacketCodec.TryReadPacket(buffer, out var pkt))
                {
                    if (pkt.MsgId == expectId)
                    {
                        return pkt;
                    }
                }

                await Task.Delay(20);
            }

            return null;
        }
    }
}
