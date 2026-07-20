#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Skynet card battle - week1 TCP test client (Windows/Linux)."""

from __future__ import print_function

import argparse
import socket
import struct
import sys
import time

if sys.version_info[0] < 3:
    print("ERROR: Python 3 is required. Try: py -3 test_client.py")
    sys.exit(1)

C2S_LoginReq = 1001
C2S_Heartbeat = 1099
S2C_LoginResp = 2001
S2C_Heartbeat = 2099


def log(msg):
    print(msg, flush=True)


def pack_packet(msg_id, payload=b""):
    body = struct.pack(">H", msg_id) + payload
    return struct.pack(">H", len(body)) + body


def read_packets(sock, buffer):
    while True:
        sock.settimeout(0.2)
        try:
            chunk = sock.recv(4096)
            if not chunk:
                raise ConnectionError("server closed connection")
            buffer.extend(chunk)
        except socket.timeout:
            break

    packets = []
    offset = 0
    total = len(buffer)

    while offset + 2 <= total:
        body_len = struct.unpack_from(">H", buffer, offset)[0]
        if offset + 2 + body_len > total:
            break
        body = bytes(buffer[offset + 2 : offset + 2 + body_len])
        msg_id = struct.unpack_from(">H", body, 0)[0]
        payload = body[2:]
        packets.append((msg_id, payload))
        offset += 2 + body_len

    del buffer[:offset]
    return packets


def pack_login_req(username, password):
    u = username.encode("utf-8")
    p = password.encode("utf-8")
    return struct.pack(">B", len(u)) + u + struct.pack(">B", len(p)) + p


def unpack_login_resp(payload):
    if len(payload) < 3:
        return {"ok": False, "message": "bad payload"}

    code = payload[0]
    msg_len = struct.unpack_from(">H", payload, 1)[0]
    message = payload[3 : 3 + msg_len].decode("utf-8", errors="replace")
    result = {"ok": code == 1, "message": message}

    if result["ok"]:
        offset = 3 + msg_len
        uid = struct.unpack_from(">I", payload, offset)[0]
        tlen = payload[offset + 4]
        token = payload[offset + 5 : offset + 5 + tlen].decode("utf-8", errors="replace")
        result["uid"] = uid
        result["token"] = token

    return result


def msg_name(msg_id):
    mapping = {
        C2S_LoginReq: "C2S_LoginReq",
        C2S_Heartbeat: "C2S_Heartbeat",
        S2C_LoginResp: "S2C_LoginResp",
        S2C_Heartbeat: "S2C_Heartbeat",
    }
    return mapping.get(msg_id, "Unknown(%d)" % msg_id)


def run(host, port, username, password):
    log("Python %s" % sys.version.split()[0])
    log("connecting to %s:%d ..." % (host, port))
    try:
        sock = socket.create_connection((host, port), timeout=10)
    except Exception as e:
        log("CONNECT FAILED: %s" % e)
        return 1

    log("connected!")
    buffer = bytearray()

    login_payload = pack_login_req(username, password)
    sock.sendall(pack_packet(C2S_LoginReq, login_payload))
    log("sent %s username=%s" % (msg_name(C2S_LoginReq), username))

    deadline = time.time() + 10
    login_result = None
    while time.time() < deadline:
        for msg_id, payload in read_packets(sock, buffer):
            log("recv %s payload_len=%d" % (msg_name(msg_id), len(payload)))
            if msg_id == S2C_LoginResp:
                login_result = unpack_login_resp(payload)
        if login_result is not None:
            break
        time.sleep(0.1)

    if login_result is None:
        log("login timeout")
        sock.close()
        return 1

    log("login result: %s" % login_result)
    if not login_result.get("ok"):
        sock.close()
        return 1

    sock.sendall(pack_packet(C2S_Heartbeat))
    log("sent %s" % msg_name(C2S_Heartbeat))

    deadline = time.time() + 10
    heartbeat_ok = False
    while time.time() < deadline:
        for msg_id, payload in read_packets(sock, buffer):
            log("recv %s payload_len=%d" % (msg_name(msg_id), len(payload)))
            if msg_id == S2C_Heartbeat and len(payload) >= 4:
                server_time = struct.unpack(">I", payload[:4])[0]
                log("server time: %d" % server_time)
                heartbeat_ok = True
        if heartbeat_ok:
            break
        time.sleep(0.1)

    sock.close()
    if not heartbeat_ok:
        log("heartbeat timeout")
        return 1

    log("test passed")
    return 0


def main():
    parser = argparse.ArgumentParser(description="Skynet card battle test client")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8888)
    parser.add_argument("--user", default="test")
    parser.add_argument("--password", default="123456")
    args = parser.parse_args()
    try:
        return run(args.host, args.port, args.user, args.password)
    except Exception as e:
        log("ERROR: %s" % e)
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
