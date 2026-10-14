#!/bin/bash
# stop play -> refresh/compile -> run builder step(s) -> play -> wait -> capture
# usage: ./cycle.sh shotname waitseconds step1.cs [step2.cs ...]
name=$1; wait=$2; shift 2
idle() { # wait until not playing / compiling
  for i in $(seq 1 40); do
    s=$(./run.sh steps/38_status.cs status | grep STATUS)
    echo "$s" | grep -q "playing=\[False\] compiling=\[False\] updating=\[False\]" && return 0
    echo "$s" | grep -q "playing=\[True\]" && ./run.sh steps/39_stop.cs stop >/dev/null
    sleep 5
  done
}
idle
./run.sh steps/00_refresh.cs refresh | tail -1
sleep 5; idle
for s in "$@"; do ./run.sh "$s" step | tail -3; idle; done
[ "$name" = "-" ] && exit 0
./run.sh steps/30_play.cs play | tail -1
sleep 12
./run.sh steps/31_state.cs state | tail -1
sleep "$wait"
echo "$name" > _preview/shot_name.txt
./run.sh steps/32_shot.cs shot | tail -1
for i in $(seq 1 30); do [ -f "_preview/$name.png" ] && break; sleep 2; done
ls -la "_preview/$name.png"
