import subprocess, time, sys
P = r"D:\Drillex\SourceArt\Enemies\SpiderBot"
def run(step, retries=10):
    for i in range(retries):
        out = subprocess.run([sys.executable, P + r"\unity_cmd.py", P + r"\unity_steps\\" + step], capture_output=True, text=True).stdout
        if out.strip() and "Unity not detected" not in out:
            print(step, "->", "\n".join(l for l in out.splitlines() if "[Log]" in l or "success" in l)); return out
        time.sleep(8)
    print(step, "-> no answer")
def shot(name):
    open(P + r"\_preview\shot_name.txt", "w").write(name)
print(run("03_play.cs"))
time.sleep(20)
run("04_spawn.cs")
time.sleep(1.5); shot("play_spawned"); run("05_report.cs")
time.sleep(3.0); shot("play_approach"); run("05_report.cs"); run("07_stop.cs"); raise SystemExit
time.sleep(3.5); shot("play_attack"); run("05_report.cs")
run("06_kill.cs")
time.sleep(0.5); shot("play_death"); run("05_report.cs")
time.sleep(2.0); shot("play_after"); run("05_report.cs")
run("07_stop.cs")
