"""通过固定本机意图验证真实前台媒体；不读网页、不发送键鼠、不提供远程接口。"""

import argparse
import json
import queue
import subprocess
import threading
import time
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--scenario", choices=["normal", "stop", "disconnect", "takeover"], default="normal")
    parser.add_argument("--cycles", type=int, choices=range(1, 6), default=3)
    args = parser.parse_args()
    executable = Path(__file__).resolve().parents[1] / "PausePal.Windows/bin/Release/net10.0-windows10.0.19041.0/PausePal.Windows.exe"
    process = subprocess.Popen([str(executable), "--pilot"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW)
    events = queue.Queue()
    history = []

    def read():
        for line in process.stdout:
            try:
                event = json.loads(line)
                history.append(event)
                events.put(event)
            except json.JSONDecodeError:
                pass

    threading.Thread(target=read, daemon=True).start()

    def send(command):
        process.stdin.write(command + "\n")
        process.stdin.flush()

    def wait(predicate, timeout=10):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                event = events.get(timeout=max(0.01, deadline - time.monotonic()))
            except queue.Empty:
                break
            if event.get("kind") == "qualification-failure":
                print(json.dumps(event, ensure_ascii=False), flush=True)
            if predicate(event):
                return event
        raise TimeoutError("未在固定时限取得期望观察，停止测试，不补发媒体操作")

    try:
        wait(lambda e: e.get("kind") == "pilot-ready")
        for index in range(args.cycles if args.scenario == "normal" else 1):
            send("START")
            send("AWAY")
            owned = wait(lambda e: e.get("phase") == "Owned")
            print(json.dumps({"步骤": "已确认自动暂停", "轮次": index + 1, "位置": owned["position"]}, ensure_ascii=False), flush=True)
            send("UNKNOWN")
            wait(lambda e: e.get("phase") == "Owned" and e.get("intent") == "UNKNOWN")
            time.sleep(0.6)
            if args.scenario == "normal":
                send("ATTENDING")
                restored = wait(lambda e: e.get("phase") == "Idle" and e.get("status", "").startswith("已确认恢复播放"))
                if restored.get("position") is None or restored["position"] <= owned["position"]:
                    raise AssertionError("没有实际进度推进证据")
                print(json.dumps({"步骤": "已确认恢复并推进", "轮次": index + 1, "位置": restored["position"]}, ensure_ascii=False), flush=True)
                send("STOP")
                wait(lambda e: e.get("status") == "已停止观影")
            else:
                checkpoint = len(history)
                if args.scenario == "takeover":
                    print("READY_FOR_TAKEOVER", flush=True)
                    wait(lambda e: e.get("phase") == "Idle", timeout=25)
                else:
                    send("STOP" if args.scenario == "stop" else "DISCONNECT")
                    wait(lambda e: e.get("phase") == "Idle" and not e.get("watching"))
                send("ATTENDING")
                time.sleep(1.5)
                if any(e.get("phase") == "Resuming" for e in history[checkpoint:]):
                    raise AssertionError("撤销后提交了恢复")
                print("通过：撤销后迟到回看没有恢复操作；实际媒体状态仍需独立核对", flush=True)
        print("本机资格场景通过；不代表 S25、普通托盘或全播放器兼容", flush=True)
    finally:
        if process.poll() is None:
            try:
                send("EXIT")
                process.wait(timeout=5)
            except (OSError, subprocess.TimeoutExpired):
                process.kill()
                process.wait()
        if process.returncode:
            raise RuntimeError(f"资格进程异常退出：{process.returncode}")


if __name__ == "__main__":
    main()
