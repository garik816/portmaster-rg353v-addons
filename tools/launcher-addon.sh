#!/bin/bash
# Sourced by the Stardew launcher just before starting the game.
rg353v_addons_stop() {
  if [ -n "${rg353v_rumble_pid:-}" ]; then
    kill "$rg353v_rumble_pid" 2>/dev/null || true
    wait "$rg353v_rumble_pid" 2>/dev/null || true
    rg353v_rumble_pid=""
  fi
  if [ -n "${rg353v_dmc_previous:-}" ]; then
    if printf '%s\n' "$rg353v_dmc_previous" > "$rg353v_dmc_governor"; then
      echo "RG353V addons: restored DMC $rg353v_dmc_previous."
      rg353v_dmc_previous=""
    else
      echo "RG353V addons: could not restore DMC $rg353v_dmc_previous." >&2
    fi
  fi
}

rg353v_addons_start() {
  rg353v_rumble_pid=""
  rg353v_dmc_previous=""
  rg353v_dmc_governor="/sys/class/devfreq/dmc/governor"
  trap rg353v_addons_stop EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM

  if [ -r "$rg353v_dmc_governor" ] && [ -w "$rg353v_dmc_governor" ]; then
    rg353v_dmc_previous="$(cat "$rg353v_dmc_governor")"
    if [ -n "$rg353v_dmc_previous" ] && printf '%s\n' performance > "$rg353v_dmc_governor"; then
      echo "RG353V addons: DMC performance enabled (previous: $rg353v_dmc_previous)."
    fi
  fi

  export RUMBLE_PERIOD="${RUMBLE_PERIOD:-20000000}"
  export RUMBLE_ACTIVE_DUTY="${RUMBLE_ACTIVE_DUTY:-10000000}"
  export RUMBLE_IDLE_DUTY="${RUMBLE_IDLE_DUTY:-20000000}"
  export RUMBLE_BUTTONS="${RUMBLE_BUTTONS:-0}"
  if [ -x "$gamedir/tools/rg353v-addons/rumble-helper" ]; then
    "$gamedir/tools/rg353v-addons/rumble-helper" &
    rg353v_rumble_pid=$!
  fi
}
