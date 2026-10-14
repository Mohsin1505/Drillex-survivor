"""Run a Unity_RunCommand C# snippet against the Drillex editor specifically.

The shared Unity MCP connection follows whichever editor is active, so this talks to the
relay in MCP mode pinned to the Drillex project path.
usage: python unity_cmd.py <file.cs> [title]
"""
import json
import os
import subprocess
import sys
import threading
import queue

RELAY = os.path.expandvars(r"%USERPROFILE%\.unity\relay\relay_win.exe")
PROJECT = r"D:\Drillex"


def main():
    code = open(sys.argv[1], encoding="utf-8").read()
    title = sys.argv[2] if len(sys.argv) > 2 else "Drillex command"
    tool = sys.argv[3] if len(sys.argv) > 3 else "Unity_RunCommand"
    p = subprocess.Popen([RELAY, "--mcp", "--project-path", PROJECT],
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                         text=True, encoding="utf-8")
    q = queue.Queue()
    threading.Thread(target=lambda: [q.put(l) for l in p.stdout], daemon=True).start()

    def send(obj):
        p.stdin.write(json.dumps(obj) + "\n")
        p.stdin.flush()

    def wait(id_, timeout):
        while True:
            line = q.get(timeout=timeout)
            try:
                msg = json.loads(line)
            except ValueError:
                continue
            if msg.get("id") == id_:
                return msg

    send({"jsonrpc": "2.0", "id": 1, "method": "initialize",
          "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                     "clientInfo": {"name": "drillex-pipeline", "version": "1"}}})
    wait(1, 60)
    send({"jsonrpc": "2.0", "method": "notifications/initialized"})
    args = {"Code": code, "Title": title} if tool == "Unity_RunCommand" else json.loads(code)
    send({"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": tool, "arguments": args}})
    msg = wait(2, 600)
    p.kill()
    res = msg.get("result") or msg.get("error")
    for c in (res or {}).get("content", []):
        if c.get("type") == "text":
            txt = c["text"]
            try:
                d = json.loads(txt)
                data = d.get("data") or {}
                print("success:", d.get("success"), d.get("error", ""))
                print(data.get("compilationLogs", ""))
                print(data.get("executionLogs", "") or txt[:4000])
            except ValueError:
                print(txt[:6000])
        elif c.get("type") == "image":
            import base64
            out = os.environ.get("UNITY_IMG_OUT", "unity_capture.png")
            open(out, "wb").write(base64.b64decode(c["data"]))
            print("image saved:", out)
        else:
            print("[non-text content]", c.get("type"))
    if not res:
        print(msg)


if __name__ == "__main__":
    main()
