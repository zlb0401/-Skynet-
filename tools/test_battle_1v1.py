#!/usr/bin/env python3
"""Two-client smoke test: login -> match -> battle ready -> play -> end turn."""
import socket
import struct
import threading
import time

HOST = "127.0.0.1"
PORT = 8888

C2S_LoginReq = 1001
C2S_MatchReq = 1002
C2S_PlayCard = 1003
C2S_EndTurn = 1004
C2S_BattleReady = 1005
C2S_Heartbeat = 1099

S2C_LoginResp = 2001
S2C_MatchResp = 2002
S2C_BattleStart = 2003
S2C_BattleState = 2004
S2C_BattleEnd = 2005


def pack(msg_id, payload=b""):
    body = struct.pack(">H", msg_id) + payload
    return struct.pack(">H", len(body)) + body


def pack_login(user, password):
    ub, pb = user.encode(), password.encode()
    return bytes([len(ub)]) + ub + bytes([len(pb)]) + pb


def read_packet(sock):
    hdr = sock.recv(2)
    if len(hdr) < 2:
        return None, None
    (blen,) = struct.unpack(">H", hdr)
    body = b""
    while len(body) < blen:
        chunk = sock.recv(blen - len(body))
        if not chunk:
            return None, None
        body += chunk
    (msg_id,) = struct.unpack(">H", body[:2])
    return msg_id, body[2:]


def client_run(user, password, results, barrier):
    s = socket.create_connection((HOST, PORT), timeout=5)
    s.settimeout(10)
    s.sendall(pack(C2S_LoginReq, pack_login(user, password)))
    mid, payload = read_packet(s)
    assert mid == S2C_LoginResp, mid
    ok = payload[0] == 1
    assert ok, payload
    uid = struct.unpack(">I", payload[3 + struct.unpack(">H", payload[1:3])[0]:][:4])[0]
    print(f"[{user}] login ok uid={uid}")

    s.sendall(pack(C2S_MatchReq))
    # may get waiting then success from other path - keep reading until match ok
    room = None
    while True:
        mid, payload = read_packet(s)
        if mid == S2C_MatchResp:
            if payload[0] == 1:
                msg_len = struct.unpack(">H", payload[1:3])[0]
                off = 3 + msg_len
                room = struct.unpack(">I", payload[off:off + 4])[0]
                print(f"[{user}] matched room={room}")
                break
            else:
                print(f"[{user}] match waiting...")
        else:
            print(f"[{user}] unexpected before match mid={mid}")

    barrier.wait()
    s.sendall(pack(C2S_BattleReady))
    print(f"[{user}] battle ready")

    # wait battle start
    while True:
        mid, payload = read_packet(s)
        if mid in (S2C_BattleStart, S2C_BattleState):
            o = 0
            room_id = struct.unpack(">I", payload[o:o+4])[0]; o += 4
            self_uid = struct.unpack(">I", payload[o:o+4])[0]; o += 4
            opp_uid = struct.unpack(">I", payload[o:o+4])[0]; o += 4
            sl = payload[o]; o += 1; o += sl
            ol = payload[o]; o += 1; o += ol
            turn_uid = struct.unpack(">I", payload[o:o+4])[0]; o += 4
            turn_no = struct.unpack(">H", payload[o:o+2])[0]; o += 2
            print(f"[{user}] battle mid={mid} self={self_uid} turn={turn_uid} turn_no={turn_no}")
            results[user] = {
                "sock": s,
                "self_uid": self_uid,
                "turn_uid": turn_uid,
                "payload": payload,
                "offset_after_turn": o,
            }
            break
        print(f"[{user}] mid={mid} waiting battle")


def main():
    results = {}
    barrier = threading.Barrier(2)
    t1 = threading.Thread(target=client_run, args=("test", "123456", results, barrier))
    t2 = threading.Thread(target=client_run, args=("demo", "123456", results, barrier))
    t1.start(); t2.start(); t1.join(); t2.join()

    assert "test" in results and "demo" in results
    # find whose turn
    actor = None
    waiter = None
    for u, info in results.items():
        if info["turn_uid"] == info["self_uid"]:
            actor = u
        else:
            waiter = u
    assert actor, results
    print(f"actor={actor} waiter={waiter}")

    # parse hand of actor
    payload = results[actor]["payload"]
    o = 0
    o += 4 + 4 + 4
    sl = payload[o]; o += 1 + sl
    ol = payload[o]; o += 1 + ol
    o += 4 + 2  # turn uid + turn no
    o += 2 + 2 + 1 + 1 + 2  # self stats
    o += 2 + 2 + 1 + 1 + 2  # opp stats
    hand_n = payload[o]; o += 1
    hand = [struct.unpack(">H", payload[o+i*2:o+i*2+2])[0] for i in range(hand_n)]
    print(f"[{actor}] hand={hand}")
    assert hand_n > 0

    s = results[actor]["sock"]
    s.sendall(pack(C2S_PlayCard, bytes([0])))
    mid, payload = read_packet(s)
    print(f"[{actor}] after play mid={mid} len={len(payload)}")
    assert mid == S2C_BattleState

    s.sendall(pack(C2S_EndTurn))
    mid, payload = read_packet(s)
    print(f"[{actor}] after end turn mid={mid}")
    assert mid == S2C_BattleState

    # waiter should also get state
    ws = results[waiter]["sock"]
    mid, payload = read_packet(ws)
    print(f"[{waiter}] got mid={mid}")
    assert mid in (S2C_BattleState, S2C_BattleStart)

    print("SMOKE OK")


if __name__ == "__main__":
    main()
