#!/usr/bin/env python3
# =============================================================================
# test_client.py
#
# Command-line test client for buridan_server. Exercises every network
# interface without needing Bonsai:
#   - command socket  (REQ  -> REP  :5555)
#   - frame socket    (PUSH -> PULL :5556)
#   - status socket   (SUB  <- PUB  :5557)
#
# Requirements: Python 3 and pyzmq  (pip install pyzmq)
#
# Examples:
#   python3 test_client.py --host 192.168.1.50 ping
#   python3 test_client.py --host 192.168.1.50 list_protocols
#   python3 test_client.py --host 192.168.1.50 upload ../protocols/classic_buridan.json
#   python3 test_client.py --host 192.168.1.50 start classic_buridan
#   python3 test_client.py --host 192.168.1.50 stop
#   python3 test_client.py --host 192.168.1.50 test_pattern
#   python3 test_client.py --host 192.168.1.50 listen 5
#   python3 test_client.py --host 192.168.1.50 stream 5
#   python3 test_client.py --host 192.168.1.50 selftest
#   python3 test_client.py --host 192.168.1.50 set_pwm_bits 8
#   python3 test_client.py --host 192.168.1.50 bitdepth_sweep
#   python3 test_client.py --host 192.168.1.50 bitdepth_sweep --rate 60 --csv sweep.csv
#   python3 test_client.py --host 192.168.1.50 bitdepth_sweep_gb --csv sweep_gb.csv
#   (run_bitdepth_tests.sh runs both sweeps with --led-pwm-lsb-nanoseconds=50)
# =============================================================================
import argparse
import csv
import json
import os
import sys
import time

import zmq

# Ring geometry (must match pi/src/RingGeometry.h).
RING_WIDTH = 384
RING_HEIGHT = 64
RING_FRAME_BYTES = RING_WIDTH * RING_HEIGHT * 3

# How long to wait for a command reply before giving up.
REPLY_TIMEOUT_MS = 2000


def send_command(context, host, port, request):
    """Sends one JSON command and returns the decoded JSON reply.

    A new REQ socket is used for every command, so a missing reply (server not
    running) never leaves the socket stuck.
    """
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.LINGER, 0)
    socket.setsockopt(zmq.RCVTIMEO, REPLY_TIMEOUT_MS)
    socket.connect(f"tcp://{host}:{port}")
    try:
        request_text = json.dumps(request)
        socket.send_string(request_text)
        reply_text = socket.recv_string()
    except zmq.Again:
        raise TimeoutError(f"no reply from tcp://{host}:{port} within {REPLY_TIMEOUT_MS} ms")
    finally:
        socket.close()
    reply = json.loads(reply_text)
    return reply


def listen(context, host, port, seconds):
    """Prints every status / event message received for the given time."""
    socket = context.socket(zmq.SUB)
    socket.setsockopt(zmq.LINGER, 0)
    socket.setsockopt(zmq.RCVTIMEO, 200)
    socket.setsockopt_string(zmq.SUBSCRIBE, "")
    socket.connect(f"tcp://{host}:{port}")
    messages = []
    end_time = time.monotonic() + seconds
    while time.monotonic() < end_time:
        try:
            parts = socket.recv_multipart()
        except zmq.Again:
            continue
        topic = parts[0].decode()
        payload = json.loads(parts[1].decode())
        messages.append((topic, payload))
        print(topic, json.dumps(payload))
    socket.close()
    return messages


def make_test_frame(offset):
    """Builds one packed-RGB ring frame: a red band that moves with 'offset'."""
    frame = bytearray(RING_FRAME_BYTES)
    for y in range(RING_HEIGHT):
        for x in range(RING_WIDTH):
            index = (y * RING_WIDTH + x) * 3
            inside_band = ((x + offset) % RING_WIDTH) < 32
            if inside_band:
                frame[index + 0] = 255
    return bytes(frame)


def stream(context, host, frame_port, seconds, rate_hz=60.0):
    """Streams a moving test pattern for the given time. Returns frames sent."""
    socket = context.socket(zmq.PUSH)
    socket.setsockopt(zmq.LINGER, 0)
    socket.connect(f"tcp://{host}:{frame_port}")
    frames_sent = 0
    period = 1.0 / rate_hz
    end_time = time.monotonic() + seconds
    while time.monotonic() < end_time:
        frame = make_test_frame(frames_sent * 4)
        socket.send(frame)
        frames_sent += 1
        time.sleep(period)
    socket.close()
    return frames_sent


def check(condition, description):
    """Prints PASS/FAIL for one self-test step and returns the condition."""
    label = "PASS" if condition else "FAIL"
    print(f"[{label}] {description}")
    return condition


def selftest(context, args, protocol_path):
    """Runs every command and checks the replies. Returns True if all pass."""
    results = []

    reply = send_command(context, args.host, args.command_port, {"cmd": "ping"})
    results.append(check(reply.get("ok") and reply.get("reply") == "pong", "ping"))

    with open(protocol_path) as protocol_file:
        protocol = json.load(protocol_file)
    protocol["name"] = "selftest_protocol"
    # Make the protocol short so the whole run takes a few seconds.
    protocol["epochs"] = [
        {"type": "stripes", "duration_s": 0.5, "params": {}},
        {"type": "grating", "duration_s": 0.5, "params": {"speed_deg_s": 90}},
    ]
    reply = send_command(context, args.host, args.command_port, {"cmd": "upload_protocol", "protocol": protocol})
    results.append(check(reply.get("ok") and reply.get("saved") == "selftest_protocol", "upload_protocol"))

    reply = send_command(context, args.host, args.command_port, {"cmd": "list_protocols"})
    results.append(check(reply.get("ok") and "selftest_protocol" in reply.get("protocols", []), "list_protocols"))

    reply = send_command(context, args.host, args.command_port, {"cmd": "get_protocol", "name": "selftest_protocol"})
    results.append(check(reply.get("ok") and len(reply["protocol"]["epochs"]) == 2, "get_protocol"))

    bad_protocol = {"name": "../escape", "epochs": []}
    reply = send_command(context, args.host, args.command_port, {"cmd": "upload_protocol", "protocol": bad_protocol})
    results.append(check(not reply.get("ok"), "upload_protocol rejects unsafe name"))

    bad_params = {"name": "bad_params", "epochs": [{"type": "grating", "duration_s": 1, "params": {"period_deg": -5}}]}
    reply = send_command(context, args.host, args.command_port, {"cmd": "upload_protocol", "protocol": bad_params})
    results.append(check(not reply.get("ok") and "period_deg" in reply.get("error", ""), "upload_protocol rejects bad params"))

    reply = send_command(context, args.host, args.command_port, {"cmd": "nonsense"})
    results.append(check(not reply.get("ok"), "unknown command is rejected"))

    # Start the protocol and listen to the events it produces.
    reply = send_command(context, args.host, args.command_port, {"cmd": "start_protocol", "name": "selftest_protocol"})
    results.append(check(reply.get("ok") and reply.get("started") == "selftest_protocol", "start_protocol"))
    messages = listen(context, args.host, args.status_port, 2.0)
    events = [payload.get("event") for topic, payload in messages if topic == "event"]
    statuses = [payload for topic, payload in messages if topic == "status"]
    results.append(check("epoch_start" in events and "protocol_end" in events, "epoch_start and protocol_end events"))
    results.append(check(len(statuses) >= 10, f"status messages received ({len(statuses)} in 2 s)"))

    # Stream frames and check that the server counted them. The server counts
    # frames since it started, so only the increase during this test is used.
    reply = send_command(context, args.host, args.command_port, {"cmd": "status"})
    received_before = reply.get("streamed_frames_received", 0)
    reply = send_command(context, args.host, args.command_port, {"cmd": "set_mode", "mode": "stream"})
    results.append(check(reply.get("ok"), "set_mode stream"))
    frames_sent = stream(context, args.host, args.frame_port, 1.0)
    time.sleep(0.3)
    reply = send_command(context, args.host, args.command_port, {"cmd": "status"})
    received_after = reply.get("streamed_frames_received", 0)
    received = received_after - received_before
    results.append(check(received > 0 and reply.get("mode") == "stream",
                         f"streamed frames received ({received} of {frames_sent})"))

    reply = send_command(context, args.host, args.command_port, {"cmd": "stop"})
    results.append(check(reply.get("ok"), "stop"))

    # Change the colour bit depth and put it back.
    status = send_command(context, args.host, args.command_port, {"cmd": "status"})
    if "pwm_bits" not in status:
        # A server built before set_pwm_bits existed: say so instead of
        # reporting confusing failures.
        results.append(check(False, "set_pwm_bits: the server is an older build without this command; "
                                    "rebuild and reinstall it on the Pi (make && make install), "
                                    "then restart it (sudo systemctl restart buridan-server)"))
    else:
        original_bits = status["pwm_bits"]
        test_bits = 8 if original_bits != 8 else 7
        reply = send_command(context, args.host, args.command_port, {"cmd": "set_pwm_bits", "bits": test_bits})
        applied = reply.get("ok") and wait_for_pwm_bits(context, args, test_bits)
        results.append(check(applied, f"set_pwm_bits {test_bits}"))
        reply = send_command(context, args.host, args.command_port, {"cmd": "set_pwm_bits", "bits": 12})
        rejected_for_range = (not reply.get("ok")) and ("between" in reply.get("error", ""))
        results.append(check(rejected_for_range, "set_pwm_bits rejects 12"))
        send_command(context, args.host, args.command_port, {"cmd": "set_pwm_bits", "bits": original_bits})

    reply = send_command(context, args.host, args.command_port, {"cmd": "delete_protocol", "name": "selftest_protocol"})
    results.append(check(reply.get("ok"), "delete_protocol"))

    passed = sum(1 for result in results if result)
    print(f"\n{passed}/{len(results)} checks passed")
    return passed == len(results)


def parse_bit_list(text):
    """Turns "11,10,8" or "11-1" into a list of PWM bit depths, in that order."""
    bits_list = []
    for part in text.split(","):
        part = part.strip()
        if "-" in part:
            first_text, last_text = part.split("-")
            first = int(first_text)
            last = int(last_text)
            step = -1 if last < first else 1
            bits_list.extend(range(first, last + step, step))
        else:
            bits_list.append(int(part))
    for bits in bits_list:
        if bits < 1 or bits > 11:
            raise ValueError(f"PWM bits must be between 1 and 11 (got {bits})")
    return bits_list


def wait_for_pwm_bits(context, args, bits, timeout_seconds=2.0):
    """Polls the status until the render thread reports 'bits'. Returns True on success."""
    end_time = time.monotonic() + timeout_seconds
    while time.monotonic() < end_time:
        status = send_command(context, args.host, args.command_port, {"cmd": "status"})
        if status.get("pwm_bits") == bits:
            return True
        time.sleep(0.05)
    return False


# Colours of the sweep grating for each colour set: (bright colour, dark colour).
# "rgb" uses all three LED colours (white on black); "gb" uses only the green
# and blue LEDs (cyan on black), so red stays off for the whole sweep.
SWEEP_COLOURS = {
    "rgb": ([255, 255, 255], [0, 0, 0]),
    "gb": ([0, 255, 255], [0, 0, 0]),
}


def bitdepth_sweep(context, args, colour_set):
    """Measures the achieved frame rate at each PWM bit depth.

    A moving grating (the most demanding standard stimulus) plays during the
    whole sweep, in the colours of 'colour_set' (a key of SWEEP_COLOURS).
    For every bit depth the client:
      1. sends set_pwm_bits and waits until the Pi reports the new value,
      2. waits 'settle' seconds so the change has fully taken effect,
      3. reads the Pi frame counter twice, 'step' seconds apart.
    The achieved frame rate is frames drawn / Pi time elapsed, so it does not
    depend on network delays. The original bit depth is restored at the end.

    The Pi never draws faster than the requested rate (--rate, at most 240 Hz),
    and never faster than the panels refresh: every frame waits for the next
    panel refresh (SwapOnVSync). So a value below the requested rate means the
    panels cannot refresh that fast at this bit depth.
    """
    bits_list = parse_bit_list(args.bits)
    target_rate_hz = args.rate
    step_seconds = args.step_seconds
    settle_seconds = 1.0

    status = send_command(context, args.host, args.command_port, {"cmd": "status"})
    if "pwm_bits" not in status:
        print("error: this buridan_server does not report pwm_bits; rebuild and reinstall it (make && make install)")
        return False
    original_bits = status["pwm_bits"]
    # Shortest PWM step of the panels; only set when the server starts.
    lsb_ns = status.get("pwm_lsb_ns")
    if args.lsb_ns is not None and lsb_ns != args.lsb_ns:
        print(f"error: the server uses {lsb_ns} ns PWM steps but {args.lsb_ns} ns were expected; "
              f"start it with --led-pwm-lsb-nanoseconds={args.lsb_ns} (run_bitdepth_tests.sh does this)")
        return False
    if not status.get("hardware", True):
        print("warning: the server runs with --no-hardware; the bit depth has no effect, "
              "so these numbers only show the render loop speed.\n")

    # A grating that keeps moving for longer than the whole sweep.
    sweep_seconds = len(bits_list) * (step_seconds + settle_seconds + 3.0) + 10.0
    bright_rgb, dark_rgb = SWEEP_COLOURS[colour_set]
    protocol = {
        "name": "bitdepth_sweep",
        "frame_rate_hz": target_rate_hz,
        "epochs": [
            {"type": "grating", "duration_s": sweep_seconds,
             "params": {"period_deg": 30, "speed_deg_s": 90, "direction": "cw", "waveform": "sine",
                        "rgb_a": bright_rgb, "rgb_b": dark_rgb}},
        ],
    }
    reply = send_command(context, args.host, args.command_port, {"cmd": "start_protocol", "protocol": protocol})
    if not reply.get("ok"):
        print(f"error: could not start the sweep stimulus: {reply.get('error')}")
        return False

    print(f"Bit depth sweep ({colour_set}): moving sine grating {bright_rgb} / {dark_rgb}, "
          f"requested {target_rate_hz:g} Hz, {step_seconds:g} s per step")
    print(f"PWM step (lsb): {lsb_ns} ns, original bit depth: {original_bits}\n")
    header = f"{'pwm_bits':>8}  {'grey levels':>11}  {'achieved Hz':>11}  {'% of requested':>14}  {'lowest 1-s Hz':>13}"
    print(header)
    print("-" * len(header))

    results = []
    success = True
    try:
        for bits in bits_list:
            reply = send_command(context, args.host, args.command_port, {"cmd": "set_pwm_bits", "bits": bits})
            applied = reply.get("ok") and wait_for_pwm_bits(context, args, bits)
            if not applied:
                print(f"{bits:>8}  could not set this bit depth: {reply.get('error', 'not applied in time')}")
                success = False
                continue
            time.sleep(settle_seconds)

            first = send_command(context, args.host, args.command_port, {"cmd": "status"})
            # Sample the Pi's own 1-second frame rate during the step to catch dips.
            one_second_rates = []
            end_time = time.monotonic() + step_seconds
            while time.monotonic() < end_time:
                time.sleep(0.5)
                sample = send_command(context, args.host, args.command_port, {"cmd": "status"})
                if sample.get("fps", 0) > 0:
                    one_second_rates.append(sample["fps"])
            last = send_command(context, args.host, args.command_port, {"cmd": "status"})

            if first.get("mode") != "protocol" or last.get("mode") != "protocol":
                print(f"{bits:>8}  the stimulus stopped during this step (mode: {last.get('mode')})")
                success = False
                break

            frames_drawn = last["frame"] - first["frame"]
            pi_seconds = last["t_mono"] - first["t_mono"]
            achieved_hz = frames_drawn / pi_seconds
            percent = 100.0 * achieved_hz / target_rate_hz
            lowest_hz = min(one_second_rates) if one_second_rates else float("nan")
            grey_levels = 2 ** bits
            results.append({"colours": colour_set, "pwm_lsb_ns": lsb_ns,
                            "pwm_bits": bits, "grey_levels": grey_levels, "requested_hz": target_rate_hz,
                            "achieved_hz": round(achieved_hz, 2), "percent_of_requested": round(percent, 1),
                            "lowest_1s_hz": round(lowest_hz, 2), "frames": frames_drawn,
                            "seconds": round(pi_seconds, 3)})
            print(f"{bits:>8}  {grey_levels:>11}  {achieved_hz:>11.1f}  {percent:>13.1f}%  {lowest_hz:>13.1f}")
    finally:
        # Always leave the arena as it was found.
        send_command(context, args.host, args.command_port, {"cmd": "set_pwm_bits", "bits": original_bits})
        send_command(context, args.host, args.command_port, {"cmd": "stop"})
        print(f"\nRestored {original_bits} PWM bits and stopped the stimulus.")

    if args.csv and results:
        with open(args.csv, "w", newline="") as csv_file:
            writer = csv.DictWriter(csv_file, fieldnames=list(results[0].keys()))
            writer.writeheader()
            writer.writerows(results)
        print(f"Results written to {args.csv}")
    return success


def main():
    parser = argparse.ArgumentParser(description="Test client for buridan_server")
    parser.add_argument("--host", default="127.0.0.1", help="Raspberry Pi address")
    parser.add_argument("--command-port", type=int, default=5555)
    parser.add_argument("--frame-port", type=int, default=5556)
    parser.add_argument("--status-port", type=int, default=5557)
    parser.add_argument("action", help="ping | status | list_protocols | upload FILE | start NAME | stop | "
                                       "test_pattern | listen SECONDS | stream SECONDS | selftest [FILE] | "
                                       "set_pwm_bits N | bitdepth_sweep | bitdepth_sweep_gb | shutdown")
    parser.add_argument("argument", nargs="?", help="argument for the action")
    # Options for bitdepth_sweep.
    parser.add_argument("--bits", default="11-1",
                        help="bitdepth_sweep: bit depths to test, in order, e.g. 11-1 or 11,8,5,3 (default: 11-1)")
    parser.add_argument("--rate", type=float, default=240.0,
                        help="bitdepth_sweep: requested frame rate in Hz, 1-240 (default: 240 = as fast as possible)")
    parser.add_argument("--step-seconds", type=float, default=5.0,
                        help="bitdepth_sweep: measurement time per bit depth (default: 5)")
    parser.add_argument("--csv", help="bitdepth_sweep: also write the results to this CSV file")
    parser.add_argument("--lsb-ns", type=int,
                        help="bitdepth_sweep: stop unless the server was started with "
                             "--led-pwm-lsb-nanoseconds equal to this value")
    args = parser.parse_args()

    context = zmq.Context()
    action = args.action

    if action == "selftest":
        # Default: the example protocol next to this script (pi/protocols),
        # found from the script's own location so it works from any folder.
        script_folder = os.path.dirname(os.path.abspath(__file__))
        default_protocol_path = os.path.join(script_folder, "..", "protocols", "classic_buridan.json")
        protocol_path = args.argument or default_protocol_path
        success = selftest(context, args, protocol_path)
        return 0 if success else 1
    if action == "bitdepth_sweep":
        success = bitdepth_sweep(context, args, "rgb")
        return 0 if success else 1
    if action == "bitdepth_sweep_gb":
        # Same sweep, with the red LEDs always off.
        success = bitdepth_sweep(context, args, "gb")
        return 0 if success else 1
    if action == "listen":
        listen(context, args.host, args.status_port, float(args.argument or 5))
        return 0
    if action == "stream":
        send_command(context, args.host, args.command_port, {"cmd": "set_mode", "mode": "stream"})
        frames_sent = stream(context, args.host, args.frame_port, float(args.argument or 5))
        print(f"sent {frames_sent} frames")
        return 0

    # Simple one-shot commands.
    if action == "upload":
        with open(args.argument) as protocol_file:
            protocol = json.load(protocol_file)
        request = {"cmd": "upload_protocol", "protocol": protocol}
    elif action == "start":
        request = {"cmd": "start_protocol", "name": args.argument}
    elif action == "test_pattern":
        request = {"cmd": "set_mode", "mode": "test_pattern"}
    elif action == "set_pwm_bits":
        request = {"cmd": "set_pwm_bits", "bits": int(args.argument)}
    else:
        request = {"cmd": action}
        if args.argument:
            request["name"] = args.argument

    reply = send_command(context, args.host, args.command_port, request)
    print(json.dumps(reply, indent=2))
    return 0 if reply.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
