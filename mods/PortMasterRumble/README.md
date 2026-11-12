# PortMaster Rumble for Stardew Valley

SMAPI mod for Stardew Valley on PortMaster-compatible RG353V / UnofficialOS systems.

The complete recovered source for this release is available in `src/PortMasterRumbleMod/ModEntry.cs`. The original source bundle did not include a .csproj; its generated compiler response file contains machine-specific paths and is intentionally not tracked.

The mod sends in-game events to the PortMaster PWM rumble helper through FIFO:

```sh
/tmp/stardew-rumble
```

It does not drive the motor directly. The launcher should start `tools/rumble-helper` in FIFO-only mode:

```sh
export RUMBLE_BUTTONS="${RUMBLE_BUTTONS:-0}"
```

## Requirements

- SMAPI mode enabled.
- `rumble-helper` running from the launcher.
- Generic Mod Config Menu is optional but supported.

If rumble stops after updating the mainline port, check the launcher: updates
can replace it and remove the helper startup while SMAPI still loads this mod.
Reapply integration using `install-stardew.py` from
[portmaster-rg353v-rumble-module](https://github.com/garik816/portmaster-rg353v-rumble-module):

```sh
python3 install-stardew.py /roms/ports/StardewValleySMAPI.sh
```

Use the actual installed launcher path. The installer backs up changed files
and starts the helper in FIFO-only mode. A mod DLL rebuild is not needed for
this launcher regression.

## Events

The mod can rumble on:

- game launched;
- day started;
- location change;
- damage / health loss;
- tool use;
- stamina drop;
- fishing bite/nibble;
- menu open/close, disabled by default.

## Generic Mod Config Menu

If `Generic Mod Config Menu` is installed, open its in-game menu and select `PortMaster Rumble`.

Configurable values:

- FIFO path;
- active PWM duty, lower is stronger on RG353V with inversed polarity;
- enable/disable tool, fishing, and menu rumble;
- pulse duration for each event;
- stamina threshold;
- cooldown ticks for repeated events.

Defaults were tuned for RG353V / UnofficialOS:

```text
ActiveDuty = 10000000
FIFO path = /tmp/stardew-rumble
```

## Manual Config

After first launch, SMAPI creates:

```text
Mods/PortMasterRumble/config.json
```

You can edit it manually if GMCM is unavailable.

## Install

Copy this folder to:

```text
stardewvalleymainline/Mods/PortMasterRumble
```

Expected files:

```text
manifest.json
PortMasterRumble.dll
README.md
```

Restart Stardew Valley after installing or updating the mod.
## Tuned Defaults

```text
ToolUseMs = 90
StaminaDropMs = 70
FishNibbleMs = 260
```
