#!/usr/bin/env bash
# =============================================================================
# run_bitdepth_tests.sh
#
# Measures the achieved frame rate at every PWM bit depth (11 down to 1), with
# the panels' shortest PWM step set to 50 ns:
#
#   1. bitdepth_sweep     - grating in white / black (red, green and blue LEDs)
#   2. bitdepth_sweep_gb  - grating in cyan / black (green and blue LEDs only,
#                           red always off)
#
# --led-pwm-lsb-nanoseconds can only be chosen when buridan_server starts, so
# this script stops the buridan-server service, starts buridan_server by hand
# with that flag, runs both sweeps, shuts it down and restarts the service.
#
# Run it ON THE PI, as the experiment user, from anywhere:
#
#   bash ~/buridan_bonsai1/pi/tools/run_bitdepth_tests.sh
#
# Extra arguments are passed to buridan_server, e.g. a GPIO slowdown that is
# also set in the service file:
#
#   bash ~/buridan_bonsai1/pi/tools/run_bitdepth_tests.sh --led-slowdown-gpio=3
#
# Results (both tables, two CSV files and the server log) are saved in
#   ~/buridan/bitdepth_results/<date>_<time>/
#
# Needs Python 3 with pyzmq on the Pi:  sudo apt install python3-zmq
# =============================================================================

# Stop at the first error, on unset variables, and on errors inside pipes.
set -euo pipefail

# Shortest PWM step used for both sweeps, in nanoseconds (library default: 130).
LSB_NANOSECONDS=50

# Folder of this script (test_client.py is next to it).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_CLIENT="${SCRIPT_DIR}/test_client.py"

# Installed server (see the Makefile 'install' target).
SERVER_BINARY="${HOME}/buridan/bin/buridan_server"
SERVICE_NAME="buridan-server"

# One results folder per run.
RUN_NAME="$(date +%Y-%m-%d_%H-%M-%S)"
RESULTS_DIR="${HOME}/buridan/bitdepth_results/${RUN_NAME}"
SERVER_LOG="${RESULTS_DIR}/server.log"
SUMMARY_FILE="${RESULTS_DIR}/summary.txt"

# --- checks before touching anything --------------------------------------------
if [ ! -x "${SERVER_BINARY}" ]; then
  echo "error: ${SERVER_BINARY} not found; run 'make && make install' in the pi/ folder first"
  exit 1
fi
if ! python3 -c "import zmq" 2> /dev/null; then
  echo "error: Python module zmq not found; install it with: sudo apt install python3-zmq"
  exit 1
fi

mkdir -p "${RESULTS_DIR}"

# Ask for the sudo password now, not in the middle of the run.
sudo -v

# Remember whether the service was running, so it can be restarted at the end.
SERVICE_WAS_ACTIVE=0
if systemctl is-active --quiet "${SERVICE_NAME}"; then
  SERVICE_WAS_ACTIVE=1
fi

SERVER_PID=""

# Runs on every exit (normal end, error or Ctrl+C): stop our server and put
# the service back the way it was.
restore() {
  if [ -n "${SERVER_PID}" ]; then
    echo
    echo "=== Stopping the test server ==="
    # A clean shutdown blanks the panels before the server exits.
    python3 "${TEST_CLIENT}" --host 127.0.0.1 shutdown > /dev/null 2>&1 || true
    # Give it 10 s to exit, then terminate it.
    for attempt in $(seq 1 20); do
      if ! ps -p "${SERVER_PID}" > /dev/null; then
        break
      fi
      sleep 0.5
    done
    if ps -p "${SERVER_PID}" > /dev/null; then
      echo "buridan_server did not exit; terminating it"
      sudo kill -TERM "${SERVER_PID}" || true
    fi
    wait "${SERVER_PID}" 2> /dev/null || true
  fi
  if [ "${SERVICE_WAS_ACTIVE}" -eq 1 ]; then
    echo "=== Restarting the ${SERVICE_NAME} service ==="
    sudo systemctl start "${SERVICE_NAME}"
  fi
}
trap restore EXIT

# --- start buridan_server with the test settings ---------------------------------
if [ "${SERVICE_WAS_ACTIVE}" -eq 1 ]; then
  echo "=== Stopping the ${SERVICE_NAME} service ==="
  sudo systemctl stop "${SERVICE_NAME}"
fi

echo "=== Starting buridan_server with --led-pwm-lsb-nanoseconds=${LSB_NANOSECONDS} $* ==="
sudo "${SERVER_BINARY}" --led-pwm-lsb-nanoseconds="${LSB_NANOSECONDS}" "$@" > "${SERVER_LOG}" 2>&1 &
SERVER_PID=$!

# Wait up to 10 s until the server answers.
SERVER_READY=0
for attempt in $(seq 1 20); do
  if python3 "${TEST_CLIENT}" --host 127.0.0.1 ping > /dev/null 2>&1; then
    SERVER_READY=1
    break
  fi
  sleep 0.5
done
if [ "${SERVER_READY}" -ne 1 ]; then
  echo "error: buridan_server did not start; its output is in ${SERVER_LOG}:"
  cat "${SERVER_LOG}"
  exit 1
fi

# --- the two sweeps -----------------------------------------------------------------
# Each sweep checks that the server really uses ${LSB_NANOSECONDS} ns steps.
# A failing sweep does not stop the other one; the exit code reports it.
EXIT_CODE=0

echo
echo "=== 1/2 Bit depth sweep, red + green + blue ==="
python3 "${TEST_CLIENT}" --host 127.0.0.1 bitdepth_sweep --lsb-ns "${LSB_NANOSECONDS}" \
  --csv "${RESULTS_DIR}/sweep_rgb.csv" | tee "${SUMMARY_FILE}" || EXIT_CODE=1

echo
echo "=== 2/2 Bit depth sweep, green + blue only (red off) ==="
echo >> "${SUMMARY_FILE}"
python3 "${TEST_CLIENT}" --host 127.0.0.1 bitdepth_sweep_gb --lsb-ns "${LSB_NANOSECONDS}" \
  --csv "${RESULTS_DIR}/sweep_gb.csv" | tee -a "${SUMMARY_FILE}" || EXIT_CODE=1

echo
echo "Results saved in ${RESULTS_DIR}"
exit "${EXIT_CODE}"
