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

    public readonly struct CaptchaChallenge
    {
        public readonly string Id;
        public readonly string Question;

        public CaptchaChallenge(string id, string question)
        {
            Id = id ?? string.Empty;
            Question = question ?? string.Empty;
        }
    }

    public readonly struct InventorySnapshot
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;
        public readonly (ushort itemId, uint count)[] Items;

        public InventorySnapshot(bool ok, string message, int gold, int dust, int diamond, int ticket, (ushort, uint)[] items)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
            Items = items ?? System.Array.Empty<(ushort, uint)>();
        }
    }

    public readonly struct ClaimRewardResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int GoldDelta;
        public readonly int DustDelta;
        public readonly int DiamondDelta;
        public readonly int TicketDelta;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;

        public ClaimRewardResult(bool ok, string message, int goldDelta, int dustDelta, int diamondDelta, int ticketDelta,
            int gold, int dust, int diamond, int ticket)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            GoldDelta = goldDelta;
            DustDelta = dustDelta;
            DiamondDelta = diamondDelta;
            TicketDelta = ticketDelta;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
        }
    }

    public readonly struct CardUpgradeEntry
    {
        public readonly string CardKey;
        public readonly byte Level;

        public CardUpgradeEntry(string cardKey, byte level)
        {
            CardKey = cardKey ?? string.Empty;
            Level = level;
        }
    }

    public readonly struct ListUpgradesResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;
        public readonly CardUpgradeEntry[] Upgrades;

        public ListUpgradesResult(bool ok, string message, int gold, int dust, int diamond, int ticket, CardUpgradeEntry[] upgrades)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
            Upgrades = upgrades ?? System.Array.Empty<CardUpgradeEntry>();
        }
    }

    public readonly struct UpgradeCardResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly string CardKey;
        public readonly byte Level;
        public readonly int DustSpent;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;

        public UpgradeCardResult(bool ok, string message, string cardKey, byte level, int dustSpent,
            int gold, int dust, int diamond, int ticket)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            CardKey = cardKey ?? string.Empty;
            Level = level;
            DustSpent = dustSpent;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
        }
    }

    public readonly struct UseItemResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;
        public readonly string GrantedCard;
        public readonly (ushort itemId, uint count)[] Items;

        public UseItemResult(bool ok, string message, int gold, int dust, int diamond, int ticket,
            string grantedCard, (ushort, uint)[] items)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
            GrantedCard = grantedCard ?? string.Empty;
            Items = items ?? System.Array.Empty<(ushort, uint)>();
        }
    }

    public readonly struct GachaPullItem
    {
        public readonly string CardKey;
        public readonly bool IsNew;
        public readonly int DustGained;
        public readonly byte Rarity;

        public GachaPullItem(string cardKey, bool isNew, int dustGained, byte rarity)
        {
            CardKey = cardKey ?? string.Empty;
            IsNew = isNew;
            DustGained = dustGained;
            Rarity = rarity;
        }
    }

    public readonly struct GachaResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int Gold;
        public readonly int Dust;
        public readonly int Diamond;
        public readonly int Ticket;
        public readonly byte Pity;
        public readonly GachaPullItem[] Items;
        public string GrantedCard => Items != null && Items.Length > 0 ? Items[0].CardKey : string.Empty;

        public GachaResult(bool ok, string message, int gold, int dust, int diamond, int ticket, byte pity, GachaPullItem[] items)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Gold = gold;
            Dust = dust;
            Diamond = diamond;
            Ticket = ticket;
            Pity = pity;
            Items = items ?? System.Array.Empty<GachaPullItem>();
        }
    }

    public readonly struct DeckSnapshot
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly string[] Deck;
        public readonly string[] Owned;

        public DeckSnapshot(bool ok, string message, string[] deck, string[] owned)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Deck = deck ?? System.Array.Empty<string>();
            Owned = owned ?? System.Array.Empty<string>();
        }
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
            return PackStr8Pair(username, password);
        }

        public static byte[] PackRegisterReq(string username, string password, string captchaId, string captchaAnswer)
        {
            var parts = new[]
            {
                Encoding.UTF8.GetBytes(username ?? string.Empty),
                Encoding.UTF8.GetBytes(password ?? string.Empty),
                Encoding.UTF8.GetBytes(captchaId ?? string.Empty),
                Encoding.UTF8.GetBytes(captchaAnswer ?? string.Empty),
            };
            foreach (var p in parts)
            {
                if (p.Length > 255)
                {
                    throw new ArgumentException("field too long");
                }
            }

            var len = 0;
            foreach (var p in parts)
            {
                len += 1 + p.Length;
            }

            var payload = new byte[len];
            var offset = 0;
            foreach (var p in parts)
            {
                payload[offset++] = (byte)p.Length;
                Buffer.BlockCopy(p, 0, payload, offset, p.Length);
                offset += p.Length;
            }

            return payload;
        }

        public static byte[] PackTokenLoginReq(string token)
        {
            var t = Encoding.UTF8.GetBytes(token ?? string.Empty);
            if (t.Length > 255)
            {
                throw new ArgumentException("token too long");
            }

            var payload = new byte[1 + t.Length];
            payload[0] = (byte)t.Length;
            Buffer.BlockCopy(t, 0, payload, 1, t.Length);
            return payload;
        }

        public static byte[] PackStr8(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? string.Empty);
            if (b.Length > 255)
            {
                throw new ArgumentException("string too long");
            }

            var payload = new byte[1 + b.Length];
            payload[0] = (byte)b.Length;
            Buffer.BlockCopy(b, 0, payload, 1, b.Length);
            return payload;
        }

        private static byte[] PackStr8Pair(string a, string b)
        {
            var u = Encoding.UTF8.GetBytes(a ?? string.Empty);
            var p = Encoding.UTF8.GetBytes(b ?? string.Empty);
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

        public static CaptchaChallenge ParseCaptchaResp(byte[] payload)
        {
            if (payload == null || payload.Length < 2)
            {
                return new CaptchaChallenge(string.Empty, string.Empty);
            }

            var o = 0;
            var id = ReadStr8(payload, ref o);
            var question = ReadStr8(payload, ref o);
            return new CaptchaChallenge(id, question);
        }

        public static InventorySnapshot ParseInventoryResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new InventorySnapshot(false, "bad payload", 0, 0, 0, 0, null);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new InventorySnapshot(false, "truncated", 0, 0, 0, 0, null);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new InventorySnapshot(false, message, 0, 0, 0, 0, null);
            }

            var o = 3 + msgLen;
            if (payload.Length < o + 17)
            {
                return new InventorySnapshot(false, "truncated wallet", 0, 0, 0, 0, null);
            }

            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o); o += 4;
            var n = payload[o++];
            if (payload.Length < o + n * 6)
            {
                return new InventorySnapshot(false, "truncated items", gold, dust, diamond, ticket, null);
            }

            var items = new (ushort, uint)[n];
            for (var i = 0; i < n; i++)
            {
                var itemId = ReadUInt16BE(payload, o); o += 2;
                var count = ReadUInt32BE(payload, o); o += 4;
                items[i] = (itemId, count);
            }

            return new InventorySnapshot(true, message, gold, dust, diamond, ticket, items);
        }

        public static byte[] PackClaimRewardReq(string token, string stageKey, string runId)
        {
            var parts = new[]
            {
                Encoding.UTF8.GetBytes(token ?? string.Empty),
                Encoding.UTF8.GetBytes(stageKey ?? string.Empty),
                Encoding.UTF8.GetBytes(runId ?? string.Empty),
            };
            var len = 0;
            foreach (var p in parts)
            {
                if (p.Length > 255)
                {
                    throw new ArgumentException("field too long");
                }

                len += 1 + p.Length;
            }

            var payload = new byte[len];
            var offset = 0;
            foreach (var p in parts)
            {
                payload[offset++] = (byte)p.Length;
                Buffer.BlockCopy(p, 0, payload, offset, p.Length);
                offset += p.Length;
            }

            return payload;
        }

        public static ClaimRewardResult ParseClaimRewardResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new ClaimRewardResult(false, "bad payload", 0, 0, 0, 0, 0, 0, 0, 0);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new ClaimRewardResult(false, "truncated", 0, 0, 0, 0, 0, 0, 0, 0);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new ClaimRewardResult(false, message, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            var o = 3 + msgLen;
            if (payload.Length < o + 24)
            {
                return new ClaimRewardResult(true, message, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            var gd = (int)ReadUInt32BE(payload, o); o += 4;
            var dd = (int)ReadUInt32BE(payload, o); o += 4;
            var diamondDelta = (int)ReadUInt32BE(payload, o); o += 4;
            var ticketDelta = (int)ReadUInt32BE(payload, o); o += 4;
            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o);
            return new ClaimRewardResult(true, message, gd, dd, diamondDelta, ticketDelta, gold, dust, diamond, ticket);
        }

        public static byte[] PackUpgradeCardReq(string token, string cardKey)
        {
            return PackStr8Pair(token, cardKey);
        }

        public static byte[] PackUseItemReq(string token, ushort itemId)
        {
            var t = Encoding.UTF8.GetBytes(token ?? string.Empty);
            if (t.Length > 255)
            {
                throw new ArgumentException("token too long");
            }

            var payload = new byte[1 + t.Length + 2];
            payload[0] = (byte)t.Length;
            Buffer.BlockCopy(t, 0, payload, 1, t.Length);
            WriteUInt16BE(payload, 1 + t.Length, itemId);
            return payload;
        }

        public static UseItemResult ParseUseItemResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new UseItemResult(false, "bad payload", 0, 0, 0, 0, string.Empty, null);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new UseItemResult(false, "truncated", 0, 0, 0, 0, string.Empty, null);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new UseItemResult(false, message, 0, 0, 0, 0, string.Empty, null);
            }

            var o = 3 + msgLen;
            if (payload.Length < o + 17)
            {
                return new UseItemResult(true, message, 0, 0, 0, 0, string.Empty, null);
            }

            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o); o += 4;
            var keyLen = payload[o++];
            if (o + keyLen + 1 > payload.Length)
            {
                return new UseItemResult(false, "truncated grant", gold, dust, diamond, ticket, string.Empty, null);
            }

            var granted = Encoding.UTF8.GetString(payload, o, keyLen);
            o += keyLen;
            var n = payload[o++];
            var items = new (ushort, uint)[n];
            for (var i = 0; i < n; i++)
            {
                if (o + 6 > payload.Length)
                {
                    return new UseItemResult(false, "truncated items", gold, dust, diamond, ticket, granted, null);
                }

                var id = ReadUInt16BE(payload, o); o += 2;
                var cnt = ReadUInt32BE(payload, o); o += 4;
                items[i] = (id, cnt);
            }

            return new UseItemResult(true, message, gold, dust, diamond, ticket, granted, items);
        }

        public static byte[] PackSaveDeckReq(string token, string[] cards)
        {
            cards ??= System.Array.Empty<string>();
            var t = Encoding.UTF8.GetBytes(token ?? string.Empty);
            if (t.Length > 255 || cards.Length > 255)
            {
                throw new ArgumentException("field too long");
            }

            var len = 1 + t.Length + 1;
            var parts = new byte[cards.Length][];
            for (var i = 0; i < cards.Length; i++)
            {
                parts[i] = Encoding.UTF8.GetBytes(cards[i] ?? string.Empty);
                if (parts[i].Length > 255)
                {
                    throw new ArgumentException("card key too long");
                }

                len += 1 + parts[i].Length;
            }

            var payload = new byte[len];
            var o = 0;
            payload[o++] = (byte)t.Length;
            Buffer.BlockCopy(t, 0, payload, o, t.Length);
            o += t.Length;
            payload[o++] = (byte)cards.Length;
            foreach (var p in parts)
            {
                payload[o++] = (byte)p.Length;
                Buffer.BlockCopy(p, 0, payload, o, p.Length);
                o += p.Length;
            }

            return payload;
        }

        public static DeckSnapshot ParseDeckResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new DeckSnapshot(false, "bad payload", null, null);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new DeckSnapshot(false, "truncated", null, null);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new DeckSnapshot(false, message, null, null);
            }

            var o = 3 + msgLen;
            string[] ReadList()
            {
                if (o >= payload.Length)
                {
                    return System.Array.Empty<string>();
                }

                var n = payload[o++];
                var list = new string[n];
                for (var i = 0; i < n; i++)
                {
                    if (o >= payload.Length)
                    {
                        return System.Array.Empty<string>();
                    }

                    var kl = payload[o++];
                    if (o + kl > payload.Length)
                    {
                        return System.Array.Empty<string>();
                    }

                    list[i] = Encoding.UTF8.GetString(payload, o, kl);
                    o += kl;
                }

                return list;
            }

            var deck = ReadList();
            var owned = ReadList();
            return new DeckSnapshot(true, message, deck, owned);
        }

        public static ListUpgradesResult ParseListUpgradesResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new ListUpgradesResult(false, "bad payload", 0, 0, 0, 0, null);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new ListUpgradesResult(false, "truncated", 0, 0, 0, 0, null);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new ListUpgradesResult(false, message, 0, 0, 0, 0, null);
            }

            var o = 3 + msgLen;
            if (payload.Length < o + 17)
            {
                return new ListUpgradesResult(true, message, 0, 0, 0, 0, null);
            }

            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o); o += 4;
            var n = payload[o++];
            var list = new CardUpgradeEntry[n];
            for (var i = 0; i < n; i++)
            {
                if (o >= payload.Length)
                {
                    return new ListUpgradesResult(false, "truncated upgrades", gold, dust, diamond, ticket, null);
                }

                var keyLen = payload[o++];
                if (o + keyLen + 1 > payload.Length)
                {
                    return new ListUpgradesResult(false, "truncated upgrade entry", gold, dust, diamond, ticket, null);
                }

                var key = Encoding.UTF8.GetString(payload, o, keyLen);
                o += keyLen;
                var level = payload[o++];
                list[i] = new CardUpgradeEntry(key, level);
            }

            return new ListUpgradesResult(true, message, gold, dust, diamond, ticket, list);
        }

        public static UpgradeCardResult ParseUpgradeCardResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new UpgradeCardResult(false, "bad payload", string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new UpgradeCardResult(false, "truncated", string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new UpgradeCardResult(false, message, string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var o = 3 + msgLen;
            if (o >= payload.Length)
            {
                return new UpgradeCardResult(true, message, string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var keyLen = payload[o++];
            if (o + keyLen + 1 + 20 > payload.Length)
            {
                return new UpgradeCardResult(false, "truncated upgrade body", string.Empty, 0, 0, 0, 0, 0, 0);
            }

            var key = Encoding.UTF8.GetString(payload, o, keyLen);
            o += keyLen;
            var level = payload[o++];
            var spent = (int)ReadUInt32BE(payload, o); o += 4;
            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o);
            return new UpgradeCardResult(true, message, key, level, spent, gold, dust, diamond, ticket);
        }

        public static byte[] PackGachaReq(string token, byte payType, byte count = 1)
        {
            var t = Encoding.UTF8.GetBytes(token ?? string.Empty);
            if (t.Length > 255) t = t.AsSpan(0, 255).ToArray();
            if (count == 0) count = 1;
            var payload = new byte[1 + t.Length + 2];
            payload[0] = (byte)t.Length;
            Buffer.BlockCopy(t, 0, payload, 1, t.Length);
            payload[1 + t.Length] = payType;
            payload[2 + t.Length] = count;
            return payload;
        }

        public static GachaResult ParseGachaResp(byte[] payload)
        {
            if (payload == null || payload.Length < 3)
            {
                return new GachaResult(false, "bad payload", 0, 0, 0, 0, 0, null);
            }

            var code = payload[0];
            var msgLen = ReadUInt16BE(payload, 1);
            if (payload.Length < 3 + msgLen)
            {
                return new GachaResult(false, "truncated", 0, 0, 0, 0, 0, null);
            }

            var message = Encoding.UTF8.GetString(payload, 3, msgLen);
            if (code != 1)
            {
                return new GachaResult(false, message, 0, 0, 0, 0, 0, null);
            }

            var o = 3 + msgLen;
            if (payload.Length < o + 16 + 2)
            {
                return new GachaResult(true, message, 0, 0, 0, 0, 0, null);
            }

            var gold = (int)ReadUInt32BE(payload, o); o += 4;
            var dust = (int)ReadUInt32BE(payload, o); o += 4;
            var diamond = (int)ReadUInt32BE(payload, o); o += 4;
            var ticket = (int)ReadUInt32BE(payload, o); o += 4;
            var pity = payload[o++];
            var n = payload[o++];
            var items = new GachaPullItem[n];
            for (var i = 0; i < n; i++)
            {
                if (o >= payload.Length) break;
                var card = ReadStr8(payload, ref o);
                if (o + 5 > payload.Length) break;
                var isNew = payload[o++] == 1;
                var dustGain = (int)ReadUInt32BE(payload, o); o += 4;
                var rarity = payload[o++];
                items[i] = new GachaPullItem(card, isNew, dustGain, rarity);
            }

            return new GachaResult(true, message, gold, dust, diamond, ticket, pity, items);
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
