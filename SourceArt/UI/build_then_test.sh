#!/bin/bash
# usage: ./build_then_test.sh "<build steps...>" <test step> <log>
builds=$1; step=$2; logf=$3
idle() { for i in $(seq 1 80); do timeout 60 python unity_cmd.py steps/38_status.cs s 2>/dev/null | grep -q "playing=\[False\] compiling=\[False\] updating=\[False\]" && return 0; sleep 5; done; }
idle; ./run.sh steps/00_refresh.cs r >/dev/null; sleep 8; idle
errs=$(timeout 100 python unity_cmd.py steps/err_args.json logs Unity_GetConsoleLogs | grep -o '"message": "Assets[^"]*' | head -5)
[ -n "$errs" ] && { echo "COMPILE ERRORS: $errs"; exit 1; }
for b in $builds; do echo "== $b"; timeout 580 python unity_cmd.py "$b" b 2>&1 | grep -v localFixed | tail -2; idle; done
[ "$step" = "-" ] && exit 0
rm -f "_preview/$logf"
timeout 250 python unity_cmd.py "$step" t 2>&1 | grep -v localFixed | tail -1
for i in $(seq 1 200); do grep -q "=== RESULT" "_preview/$logf" 2>/dev/null && break; sleep 5; done
cat "_preview/$logf"
