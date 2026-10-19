# Protocol format and network interface

This document is the reference for everything exchanged between the Windows PC
(Bonsai) and the Raspberry Pi (`buridan_server`):

1. [Ring geometry and angles](#1-ring-geometry-and-angles)
2. [Protocol files](#2-protocol-files)
3. [Network interface](#3-network-interface)
4. [Experiment data files](#4-experiment-data-files)

---

## 1. Ring geometry and angles

The six 64 × 64 panels form a ring that is **384 pixels around and 64 pixels
high**. Stimuli are described in *ring coordinates*:

- `x` goes around the ring, 0 … 383 (**384 px = 360°, 1 px = 0.9375°**)
- `y` goes from the top row (0) to the bottom row (63)
- **0°** is the left edge of ring column 0; angles grow with `x`
- motion directions are **`cw` / `ccw` seen from above**; how this maps to
  pixel direction is set by `x_increases_clockwise` in `arena_config.json`

How ring panels map to the hardware (chains 1–3, two panels each) is set in
`~/buridan/arena_config.json` on the Pi. Use the test pattern
(`buridan_server --test-pattern`) to check it; see `pi/src/TestPattern.h`.

---

## 2. Protocol files

A protocol is a JSON file named `<name>.json`. The same format is used on the
PC (`bonsai/protocols`) and on the Pi (`~/buridan/protocols`).

```json
{
  "name": "stripes_then_grating",
  "schema_version": 1,
  "frame_rate_hz": 60,
  "epochs": [
    { "type": "stripes", "duration_s": 300, "params": { "centers_deg": [0, 180], "width_deg": 11.25 } },
    { "type": "grating", "duration_s": 60,
      "params": { "period_deg": 30, "speed_deg_s": 45, "direction": "cw", "waveform": "square" } }
  ]
}
```

| Field | Type | Required | Default | Rule |
|---|---|---|---|---|
| `name` | string | yes | | letters, digits, `_`, `-`; 1–64 characters (also the file name) |
| `schema_version` | integer | no | 1 | must be 1 |
| `frame_rate_hz` | number | no | 60 | 1 – 240 |
| `epochs` | array | yes | | at least one epoch, played in order |

Each **epoch**:

| Field | Type | Required | Rule |
|---|---|---|---|
| `type` | string | yes | `"stripes"`, `"grating"` or `"blank"` |
| `duration_s` | number | yes | > 0 |
| `params` | object | no | parameters of the stimulus type (below); missing ones take the default |

Colours are `[r, g, b]` arrays with integers 0–255.

### `stripes` — classic Buridan stimulus

| Parameter | Default | Meaning |
|---|---|---|
| `centers_deg` | `[0, 180]` | centre angle of every stripe |
| `width_deg` | `11.25` | stripe width (12 px) |
| `stripe_rgb` | `[0, 0, 0]` | stripe colour |
| `background_rgb` | `[255, 255, 255]` | background colour |
| `stripe_top_px` | `0` | first row of the stripes |
| `stripe_bottom_px` | `63` | last row of the stripes (`top ≤ bottom ≤ 63`) |

### `grating` — rotating grating (optomotor)

| Parameter | Default | Meaning |
|---|---|---|
| `period_deg` | `30` | spatial period (one bright + one dark bar), > 0 |
| `speed_deg_s` | `0` | rotation speed, ≥ 0 |
| `direction` | `"cw"` | `"cw"` or `"ccw"` seen from above |
| `waveform` | `"square"` | `"square"` (hard bars) or `"sine"` (smooth) |
| `duty_cycle` | `0.5` | square only: fraction of each period in `rgb_a`, 0–1 |
| `phase_deg` | `0` | starting offset of the pattern |
| `rgb_a` | `[255, 255, 255]` | first colour |
| `rgb_b` | `[0, 0, 0]` | second colour |

### `blank`

| Parameter | Default | Meaning |
|---|---|---|
| `rgb` | `[0, 0, 0]` | uniform colour |

Both sides (C++ `pi/src/Protocol.cpp` and C# `ProtocolDefinition.Validate`)
apply exactly these rules, and use the same defaults when reading a file. The
Bonsai protocol editor always writes every parameter explicitly.

**Timing.** Epoch boundaries are computed from the protocol start time plus the
summed durations, so timing errors do not accumulate. Moving stimuli use the
real elapsed time, so their speed is correct even if a frame is late.

---

## 3. Network interface

`buridan_server` opens three ZeroMQ sockets (default ports):

| Port | Pattern (Pi side) | Purpose |
|---|---|---|
| 5555 | REP | commands from the PC (JSON request → JSON reply) |
| 5556 | PULL | frames streamed from the PC |
| 5557 | PUB | status (10 Hz) and events |

### 3.1 Commands (port 5555)

Each request is one JSON string with a `cmd` field. Each reply is one JSON
string with `"ok": true|false`, `"cmd"`, and `"error"` when `ok` is false.

| `cmd` | Extra request fields | Reply fields |
|---|---|---|
| `ping` | | `reply: "pong"`, `version`, `t_mono`, `t_wall` |
| `status` | | `mode`, `frame`, `fps`, `protocol`, `epoch`, `epoch_type`, `streamed_frames_received`, `streamed_frames_rejected`, `pwm_bits`, `pwm_lsb_ns` (`--led-pwm-lsb-nanoseconds`, fixed at startup), `hardware` (false with `--no-hardware`), `t_mono`, `t_wall` |
| `list_protocols` | | `protocols: [names]` |
| `get_protocol` | `name` | `protocol: {...}` |
| `upload_protocol` | `protocol: {...}` | `saved: name` (validated, then written atomically) |
| `delete_protocol` | `name` | `deleted: name` |
| `start_protocol` | `name` **or** `protocol: {...}` | `started: name`, `protocol: {...}` (exactly what is played) |
| `stop` | | stops any protocol/stream, blanks the ring |
| `set_mode` | `mode`: `"blank"`, `"stream"` or `"test_pattern"` | `mode` |
| `set_pwm_bits` | `bits`: 1–11 | `pwm_bits`. Colour bit depth of the panels (2^bits grey levels per channel; fewer bits = faster refresh). Applied before the next frame without interrupting what is playing; the `pwm_bits` status field confirms it. Not saved: the server restarts with `--led-pwm-bits` (default 11). |
| `shutdown` | | stops `buridan_server` (systemd does **not** restart it after a clean exit) |

Example with the test client: `python3 pi/tools/test_client.py --host <pi> start classic_buridan`.

The Bonsai operators open a new socket for every command and wait at most
`ReplyTimeoutMilliseconds` (default 2 s), so an unreachable Pi never blocks the
workflow.

### 3.2 Status and events (port 5557)

Every message has two parts: a topic (`"status"` or `"event"`) and a JSON
payload. All payloads contain `t_mono` (seconds since the server started) and
`t_wall` (Unix time on the Pi).

- `status`: same fields as the `status` command reply, 10 times per second.
- `event`, with `event` set to one of:

| `event` | Other fields | When |
|---|---|---|
| `mode_changed` | `mode`, `frame` | display mode changed |
| `protocol_start` | `protocol`, `frame` | a protocol starts |
| `epoch_start` | `protocol`, `epoch`, `epoch_type`, `frame` | an epoch becomes visible (`frame` = first frame showing it) |
| `protocol_end` | `protocol`, `frame` | the last epoch finished |
| `protocol_stopped` | `protocol`, `epoch`, `frame` | a protocol was interrupted (stop / new mode) |
| `protocol_saved` | `protocol` | a protocol was uploaded |
| `pwm_bits_changed` | `pwm_bits`, `frame` | the colour bit depth was changed |

### 3.3 Streamed frames (port 5556)

One single-part message per frame: exactly **73 728 bytes** = 384 × 64 × 3,
packed RGB, row-major (`index = (y × 384 + x) × 3 + channel`, channel 0 = red).
Other sizes are counted as rejected. Only the newest frame is kept, and frames
are only shown in `stream` mode. In Bonsai, `StreamRingFrames` converts
384 × 64 BGR or grayscale images to this format.

---

## 4. Experiment data files

Each run started with **Start experiment** creates
`<Data folder>/<yyyy-MM-dd_HH-mm-ss>_<AnimalId>_<protocol>/` containing:

| File | Content |
|---|---|
| `session.json` | animal, experimenter, notes, protocol name and duration, Pi host, start/stop time, stop reason |
| `protocol.json` | exact copy of the protocol that was played |
| `video.avi` | raw camera video |
| `camera_frames.csv` | `frame_index, t_session_s, pc_time` (one line per video frame) |
| `tracking.csv` | `frame_index, t_session_s, pc_time, centroid_x, centroid_y, orientation_rad, area, major_axis, minor_axis` |
| `pi_messages.csv` | `t_session_s, pc_time, topic, event, payload_json` (every Pi status/event) |

**Aligning data.** `t_session_s` is measured with one stopwatch on the PC for
all files, so video frames, tracking and Pi events share a time base. Use the
`epoch_start` lines of `pi_messages.csv` to find when each stimulus started.
Their `frame` (Pi frame counter) and `t_mono` fields give the timing on the Pi.

When no animal is found in a frame, `tracking.csv` contains `NaN` coordinates
and area 0.
