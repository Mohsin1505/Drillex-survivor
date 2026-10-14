#!/bin/bash
# usage: ./playshot.sh name wait [openstep]  -> plays current (or opened) scene, captures after wait seconds
name=$1; wait=$2; log=_preview/play.log; : > $log
[ -n "$3" ] && ./run.sh "$3" open >> $log
./run.sh steps/30_play.cs play >> $log
for i in $(seq 1 60); do ./run.sh steps/38_status.cs s | grep -q "playing=\[True\]" && break; sleep 3; done
./run.sh steps/31_state.cs st | tail -1 >> $log
sleep "$wait"
echo "$name" > _preview/shot_name.txt
./run.sh steps/32_shot.cs shot | tail -1 >> $log
for i in $(seq 1 40); do [ -f "_preview/$name.png" ] && break; sleep 2; done
echo "PLAY DONE" >> $log
