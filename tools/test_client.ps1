# Skynet card battle - PowerShell test client (no Python required)
param(
    [string]$ServerHost = "127.0.0.1",
    [int]$Port = 8888,
    [string]$User = "test",
    [string]$Password = "123456"
)

$ErrorActionPreference = "Stop"

function Write-Log($msg) {
    Write-Host $msg
}

function Pack-UInt16BE([int]$v) {
    [byte[]]@( [byte](($v -shr 8) -band 0xFF), [byte]($v -band 0xFF) )
}

function Pack-UInt32BE([int]$v) {
    [byte[]]@(
        [byte](($v -shr 24) -band 0xFF),
        [byte](($v -shr 16) -band 0xFF),
        [byte](($v -shr 8) -band 0xFF),
        [byte]($v -band 0xFF)
    )
}

function Pack-Packet([int]$msgId, [byte[]]$payload) {
    if ($null -eq $payload) { $payload = [byte[]]@() }
    $body = (Pack-UInt16BE $msgId) + $payload
    return (Pack-UInt16BE $body.Length) + $body
}

function Pack-LoginReq([string]$username, [string]$password) {
    $u = [System.Text.Encoding]::UTF8.GetBytes($username)
    $p = [System.Text.Encoding]::UTF8.GetBytes($password)
    return ([byte[]]@([byte]$u.Length)) + $u + ([byte[]]@([byte]$p.Length)) + $p
}

function Read-UInt16BE([byte[]]$buf, [int]$offset) {
    return ([int]$buf[$offset] -shl 8) -bor [int]$buf[$offset + 1]
}

function Read-UInt32BE([byte[]]$buf, [int]$offset) {
    return ([int]$buf[$offset] -shl 24) -bor ([int]$buf[$offset + 1] -shl 16) -bor ([int]$buf[$offset + 2] -shl 8) -bor [int]$buf[$offset + 3]
}

function Read-Exact([System.IO.Stream]$stream, [int]$count) {
    $buf = New-Object byte[] $count
    $offset = 0
    while ($offset -lt $count) {
        $n = $stream.Read($buf, $offset, $count - $offset)
        if ($n -le 0) {
            throw "server closed connection"
        }
        $offset += $n
    }
    return $buf
}

function Read-Packet([System.IO.Stream]$stream) {
    $lenBuf = Read-Exact $stream 2
    $bodyLen = Read-UInt16BE $lenBuf 0
    $body = Read-Exact $stream $bodyLen
    $msgId = Read-UInt16BE $body 0
    if ($body.Length -gt 2) {
        $payload = $body[2..($body.Length - 1)]
    } else {
        $payload = [byte[]]@()
    }
    return $msgId, $payload
}

function Parse-LoginResp([byte[]]$payload) {
    if ($payload.Length -lt 3) {
        return @{ ok = $false; message = "bad payload (len=$($payload.Length))" }
    }
    $code = $payload[0]
    $msgLen = Read-UInt16BE $payload 1
    if ($payload.Length -lt (3 + $msgLen)) {
        return @{ ok = $false; message = "truncated payload" }
    }
    $message = [System.Text.Encoding]::UTF8.GetString($payload, 3, $msgLen)
    $result = @{ ok = ($code -eq 1); message = $message }
    if ($result.ok) {
        $offset = 3 + $msgLen
        $result.uid = Read-UInt32BE $payload $offset
        $tlen = [int]$payload[$offset + 4]
        $result.token = [System.Text.Encoding]::UTF8.GetString($payload, $offset + 5, $tlen)
    }
    return $result
}

try {
    Write-Log "=== Skynet Card Battle Test ==="
    Write-Log "connecting to ${ServerHost}:${Port} ..."

    $client = New-Object System.Net.Sockets.TcpClient
    $client.ReceiveTimeout = 10000
    $client.SendTimeout = 10000
    $client.Connect($ServerHost, $Port)
    $stream = $client.GetStream()
    Write-Log "connected!"

    $loginPacket = Pack-Packet 1001 (Pack-LoginReq $User $Password)
    $stream.Write($loginPacket, 0, $loginPacket.Length)
    $stream.Flush()
    Write-Log "sent LoginReq user=$User (packet_len=$($loginPacket.Length))"

    $msgId, $payload = Read-Packet $stream
    Write-Log "recv msg_id=$msgId payload_len=$($payload.Length)"

    if ($msgId -ne 2001) {
        Write-Log "unexpected msg_id=$msgId, expected 2001"
        exit 1
    }

    $loginResult = Parse-LoginResp $payload
    Write-Log ("login result: ok={0} uid={1} token={2} msg={3}" -f $loginResult.ok, $loginResult.uid, $loginResult.token, $loginResult.message)
    if (-not $loginResult.ok) { exit 1 }

    $hbPacket = Pack-Packet 1099 ([byte[]]@())
    $stream.Write($hbPacket, 0, $hbPacket.Length)
    $stream.Flush()
    Write-Log "sent Heartbeat"

    $msgId, $payload = Read-Packet $stream
    Write-Log "recv msg_id=$msgId payload_len=$($payload.Length)"

    if ($msgId -ne 2099 -or $payload.Length -lt 4) {
        Write-Log "unexpected heartbeat response"
        exit 1
    }

    $serverTime = Read-UInt32BE $payload 0
    Write-Log "server time: $serverTime"

    $stream.Close()
    $client.Close()

    Write-Log "test passed"
    exit 0
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}
