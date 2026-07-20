using System;
using System.Collections.Generic;
using System.Text;

namespace CardBattle.Network
{
    public readonly struct Packet
    {
        public readonly ushort MsgId;
        public readonly byte[] Payload;

        public Packet(ushort msgId, byte[] payload)
        {
            MsgId = msgId;
            Payload = payload ?? Array.Empty<byte>();
        }
    }

    public readonly struct MatchResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly uint RoomId;
        public readonly uint OpponentUid;
        public readonly string OpponentName;

        public MatchResult(bool ok, string message, uint roomId, uint opponentUid, string opponentName)
        {
            Ok = ok;
            Message = message;
            RoomId = roomId;
            OpponentUid = opponentUid;
            OpponentName = opponentName;
        }
    }

    public readonly struct LoginResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly uint Uid;
        public readonly string Token;

        public LoginResult(bool ok, string message, uint uid, string token)
        {
            Ok = ok;
            Message = message;
            Uid = uid;
            Token = token;
        }
    }

    public readonly struct BattleEndResult
    {
        public readonly uint WinnerUid;
        public readonly string Message;

        public BattleEndResult(uint winnerUid, string message)
        {
            WinnerUid = winnerUid;
            Message = message ?? string.Empty;
        }
    }

    public sealed class BattleStateView
    {
        public uint RoomId;
        public uint SelfUid;
        public uint OppUid;
        public string SelfName = string.Empty;
        public string OppName = string.Empty;
        public uint TurnUid;
        public ushort TurnNo;
        public short SelfHp;
        public short SelfMaxHp;
        public byte SelfEnergy;
        public byte SelfMaxEnergy;
        public ushort SelfArmor;
        public short OppHp;
        public short OppMaxHp;
        public byte OppEnergy;
        public byte OppMaxEnergy;
        public ushort OppArmor;
        public ushort[] Hand = Array.Empty<ushort>();
        public byte OppHandCount;
        public byte DrawCount;
        public byte DiscardCount;
        public bool Finished;
        public uint WinnerUid;
        public string LastEvent = string.Empty;
    }

    public static class PacketCodec
    {
        private static readonly Dictionary<ushort, string> CardNames = new()
        {
            { 1, "猛击" },
            { 2, "防御" },
            { 3, "重击" },
            { 4, "专注" },
        };

        public static string GetCardName(ushort cardId) =>
            CardNames.TryGetValue(cardId, out var n) ? n : $"卡#{cardId}";

        public static byte[] Pack(ushort msgId, byte[] payload = null)
        {
            payload ??= Array.Empty<byte>();
            var body = new byte[2 + payload.Length];
            WriteUInt16BE(body, 0, msgId);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, body, 2, payload.Length);
            }

            var packet = new byte[2 + body.Length];
            WriteUInt16BE(packet, 0, (ushort)body.Length);
            Buffer.BlockCopy(body, 0, packet, 2, body.Length);
            return packet;
        }

        public static byte[] PackLoginReq(string username, string password)
        {
            var u = Encoding.UTF8.GetBytes(username);
            var p = Encoding.UTF8.GetBytes(password);
            if (u.Length > 255 || p.Length > 255)
            {
                throw new ArgumentException("username or password too long");
            }

            var payload = new byte[1 + u.Length + 1 + p.Length];
            var offset = 0;
            payload[offset++] = (byte)u.Length;
            Buffer.BlockCopy(u, 0, payload, offset, u.Length);
            offset += u.Length;
            payload[offset++] = (byte)p.Length;
            Buffer.BlockCopy(p, 0, payload, offset, p.Length);
            return payload;
        }

        public static byte[] PackPlayCard(byte handIndex) => new[] { handIndex };

        public static LoginResult ParseLoginResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new LoginResult(false, "bad payload", 0, string.Empty);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new LoginResult(false, "truncated payload", 0, string.Empty);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new LoginResult(false, message, 0, string.Empty);
            }

            var offset = 3 + msgLen;
            if (payload.Length < offset + 5)
            {
                return new LoginResult(false, "missing token", 0, string.Empty);
            }

            var uid = ReadUInt32BE(payload, offset);
            var tokenLen = payload[offset + 4];
            if (payload.Length < offset + 5 + tokenLen)
            {
                return new LoginResult(false, "truncated token", 0, string.Empty);
            }

            var token = Encoding.UTF8.GetString(payload, offset + 5, tokenLen);
            return new LoginResult(true, message, uid, token);
        }

        public static MatchResult ParseMatchResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new MatchResult(false, "bad payload", 0, 0, string.Empty);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new MatchResult(false, "truncated payload", 0, 0, string.Empty);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new MatchResult(false, message, 0, 0, string.Empty);
            }

            var offset = 3 + msgLen;
            if (payload.Length < offset + 9)
            {
                return new MatchResult(false, "missing opponent", 0, 0, string.Empty);
            }

            var roomId = ReadUInt32BE(payload, offset);
            var opponentUid = ReadUInt32BE(payload, offset + 4);
            var nameLen = payload[offset + 8];
            var opponentName = Encoding.UTF8.GetString(payload, offset + 9, nameLen);
            return new MatchResult(true, message, roomId, opponentUid, opponentName);
        }

        public static BattleStateView ParseBattleState(byte[] payload)
        {
            var view = new BattleStateView();
            if (payload == null || payload.Length < 20)
            {
                return view;
            }

            try
            {
                var o = 0;
                view.RoomId = ReadUInt32BE(payload, o); o += 4;
                view.SelfUid = ReadUInt32BE(payload, o); o += 4;
                view.OppUid = ReadUInt32BE(payload, o); o += 4;
                view.SelfName = ReadStr8(payload, ref o);
                view.OppName = ReadStr8(payload, ref o);
                if (o + 4 + 2 + 2 + 2 + 1 + 1 + 2 + 2 + 2 + 1 + 1 + 2 + 1 > payload.Length)
                {
                    view.SelfUid = 0;
                    return view;
                }

                view.TurnUid = ReadUInt32BE(payload, o); o += 4;
                view.TurnNo = ReadUInt16BE(payload, o); o += 2;
                view.SelfHp = ReadInt16BE(payload, o); o += 2;
                view.SelfMaxHp = ReadInt16BE(payload, o); o += 2;
                view.SelfEnergy = payload[o++]; view.SelfMaxEnergy = payload[o++];
                view.SelfArmor = ReadUInt16BE(payload, o); o += 2;
                view.OppHp = ReadInt16BE(payload, o); o += 2;
                view.OppMaxHp = ReadInt16BE(payload, o); o += 2;
                view.OppEnergy = payload[o++]; view.OppMaxEnergy = payload[o++];
                view.OppArmor = ReadUInt16BE(payload, o); o += 2;

                if (o >= payload.Length)
                {
                    view.SelfUid = 0;
                    return view;
                }

                var handN = payload[o++];
                if (o + handN * 2 + 3 + 4 > payload.Length)
                {
                    view.SelfUid = 0;
                    return view;
                }

                view.Hand = new ushort[handN];
                for (var i = 0; i < handN; i++)
                {
                    view.Hand[i] = ReadUInt16BE(payload, o); o += 2;
                }

                view.OppHandCount = payload[o++];
                view.DrawCount = payload[o++];
                view.DiscardCount = payload[o++];
                view.Finished = payload[o++] == 1;
                view.WinnerUid = ReadUInt32BE(payload, o); o += 4;
                view.LastEvent = ReadStr8(payload, ref o);
                return view;
            }
            catch (System.Exception)
            {
                view.SelfUid = 0;
                return view;
            }
        }

        public static BattleEndResult ParseBattleEnd(byte[] payload)
        {
            if (payload == null || payload.Length < 5)
            {
                return new BattleEndResult(0, string.Empty);
            }

            var o = 0;
            var winner = ReadUInt32BE(payload, o); o += 4;
            var msg = ReadStr8(payload, ref o);
            return new BattleEndResult(winner, msg);
        }

        public static bool TryReadPacket(List<byte> buffer, out Packet packet)
        {
            packet = default;
            if (buffer.Count < 2)
            {
                return false;
            }

            var bodyLen = ReadUInt16BE(buffer, 0);
            if (buffer.Count < 2 + bodyLen)
            {
                return false;
            }

            var body = buffer.GetRange(2, bodyLen).ToArray();
            buffer.RemoveRange(0, 2 + bodyLen);

            if (body.Length < 2)
            {
                return false;
            }

            var msgId = ReadUInt16BE(body, 0);
            byte[] payload;
            if (body.Length > 2)
            {
                payload = new byte[body.Length - 2];
                Buffer.BlockCopy(body, 2, payload, 0, payload.Length);
            }
            else
            {
                payload = Array.Empty<byte>();
            }

            packet = new Packet(msgId, payload);
            return true;
        }

        public static ushort ReadUInt16BE(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        public static ushort ReadUInt16BE(List<byte> data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        public static short ReadInt16BE(byte[] data, int offset)
        {
            return (short)((data[offset] << 8) | data[offset + 1]);
        }

        public static uint ReadUInt32BE(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24)
                | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8)
                | data[offset + 3];
        }

        public static void WriteUInt16BE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        private static string ReadStr8(byte[] data, ref int offset)
        {
            if (offset >= data.Length)
            {
                return string.Empty;
            }

            var len = data[offset++];
            if (offset + len > data.Length)
            {
                return string.Empty;
            }

            var s = Encoding.UTF8.GetString(data, offset, len);
            offset += len;
            return s;
        }
    }
}
