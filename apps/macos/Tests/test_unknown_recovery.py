"""真实前台媒体回归：先启动receiver-test并准备播放；仅向loopback发送固定观看意图。"""
import hashlib
import json
import queue
import socket
import ssl
import subprocess
import sys
import threading
import time
import uuid

port, fingerprint, probe = sys.argv[1:4]
rounds = int(sys.argv[4]) if len(sys.argv) > 4 else 3
assert 1 <= rounds <= 5
ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE  # 自签资格证书仍须下方固定指纹，且仅连接loopback。
ctx.minimum_version = ssl.TLSVersion.TLSv1_2
conn = ctx.wrap_socket(socket.create_connection(('127.0.0.1', int(port)), timeout=5), server_hostname='LookPause-Pilot')
assert hashlib.sha256(conn.getpeercert(binary_form=True)).hexdigest() == fingerprint
conn.settimeout(None)
frames = queue.Queue()
session = None
sequence = 0
last_ping = time.monotonic()

def read():
    try:
        with conn.makefile('rb') as stream:
            while line := stream.readline(4097):
                assert len(line) <= 4096
                frames.put(json.loads(line))
    finally:
        frames.put({'error': 'CONNECTION_CLOSED'})

threading.Thread(target=read, daemon=True).start()

def send(kind, **fields):
    global sequence
    if session:
        sequence += 1
        fields.update(session=session, seq=sequence)
    conn.sendall((json.dumps(dict(v=1, kind=kind, **fields)) + '\n').encode())
    return sequence

def wait_for(predicate, seconds=6):
    global last_ping
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if session and time.monotonic() - last_ping >= 2:
            send('ping'); last_ping = time.monotonic()
        try:
            frame = frames.get(timeout=0.2)
        except queue.Empty:
            continue
        if frame.get('kind') == 'status':
            print(json.dumps({'status': frame['status']}, ensure_ascii=False), flush=True)
        assert frame.get('error') != 'CONNECTION_CLOSED', '资格链路断开'
        if predicate(frame):
            return frame
    raise AssertionError('未观察到预期裁决')

def status(text):
    return wait_for(lambda frame: text in frame.get('status', ''))

def snapshot():
    result = subprocess.run([probe, 'generic-state'], capture_output=True, text=True, check=True)
    return json.loads(result.stdout)

def same_pause(first):
    latest = snapshot()
    assert latest['hint'] == 'PAUSED', latest['hint']
    assert first['contextHash'] == latest['contextHash']
    assert abs(first['progressSeconds'] - latest['progressSeconds']) <= 0.05

try:
    send('hello', deviceId=str(uuid.uuid4()), name='Local unknown regression')
    session = wait_for(lambda frame: frame.get('kind') == 'ready')['session']
    send('start'); status('等待观看方向')
    for index in range(rounds):
        send('intent', intent='AWAY')
        if index == 1:
            status('正在确认暂停')
            send('intent', intent='UNKNOWN')
            status('识别暂时未知，视频保持暂停')
        else:
            status('已自动暂停，等待回看')
        paused = snapshot()
        send('intent', intent='UNKNOWN')
        unknown = status('未知')['status']
        assert '取消' not in unknown, '旧故障：UNKNOWN删除自动暂停归属'
        time.sleep(0.8)
        same_pause(paused)
        send('intent', intent='UNKNOWN'); status('未知')
        send('intent', intent='AWAY')  # 仍是本工具暂停，不能重新授予或重复暂停。
        send('intent', intent='ATTENDING')
        wait_for(lambda frame: frame.get('status', '').startswith('已恢复播放') and '正在' not in frame['status'])
        first = snapshot()
        assert first['progressSeconds'] < paused['progressSeconds'], '恢复后未观察到实际回退'
        time.sleep(1.1); second = snapshot()
        assert second['hint'] != 'PAUSED', '恢复后仍暂停'
        assert second['progressSeconds'] > first['progressSeconds'], '实际进度未继续'
        send('intent', intent='ATTENDING'); status('看向屏幕')
        print(json.dumps({'round': index + 1, 'result': 'PASS', 'paused': paused['progressSeconds'], 'afterRewind': first['progressSeconds'], 'playing': second['progressSeconds']}, ensure_ascii=False), flush=True)
    send('intent', intent='AWAY'); status('已自动暂停，等待回看')
    paused = snapshot()
    send('intent', intent='UNKNOWN'); status('未知')
    send('stop'); status('已停止观影')
    late = send('intent', intent='ATTENDING')
    rejected = wait_for(lambda frame: frame.get('kind') == 'error' and frame.get('seq') == late)
    assert rejected.get('error') == 'NOT_WATCHING_OR_INVALID_INTENT', 'STOP后未按契约拒绝回看'
    same_pause(paused)
    print(json.dumps({'unknownStop': 'PASS', 'rounds': rounds}, ensure_ascii=False), flush=True)
finally:
    conn.close()
