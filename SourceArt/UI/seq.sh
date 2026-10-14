#!/bin/bash
# run steps sequentially, waiting for idle editor between them; log to _preview/seq.log
log=_preview/seq.log; : > $log
idle() { for i in $(seq 1 60); do s=$(./run.sh steps/38_status.cs status | grep STATUS); echo "$s" | grep -q "playing=\[False\] compiling=\[False\] updating=\[False\]" && return 0; echo "$s" | grep -q "playing=\[True\]" && ./run.sh steps/39_stop.cs stop >/dev/null; sleep 5; done; }
idle; ./run.sh steps/00_refresh.cs r >> $log; sleep 8; idle
for s in "$@"; do echo "== $s" >> $log; ./run.sh "$s" step 2>&1 | grep -v localFixedCode | cut -c1-1500 | tail -6 >> $log; idle; done
echo "SEQ DONE" >> $log
