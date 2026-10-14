#!/bin/bash
# retry wrapper: unity relay is unavailable during domain reloads
for i in $(seq 1 12); do
  out=$(python unity_cmd.py "$@" 2>&1)
  if echo "$out" | grep -q "Unity not detected\|^$" || [ -z "$(echo "$out" | tr -d '[:space:]')" ]; then sleep 10; continue; fi
  echo "$out"; exit 0
done
echo "$out"
