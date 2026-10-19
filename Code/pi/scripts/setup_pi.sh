#!/usr/bin/env bash
# =============================================================================
# setup_pi.sh
#
# One-time setup of a Raspberry Pi 4 for the Buridan arena.
# Run it on the Pi, as the experiment user (default: buridan), from the
# pi/ folder of this repository:
#
#   cd ~/buridan_bonsai1/pi
#   bash scripts/setup_pi.sh
#
# What it does:
#   1. installs the build tools and libraries (ZeroMQ, JSON, pyzmq for the tests)
#   2. clones and builds the rpi-rgb-led-matrix library in ~/rpi-rgb-led-matrix
#   3. disables the on-board sound card, which conflicts with the LED driver
#   4. builds buridan_server and installs it into ~/buridan
#   5. installs the buridan-server systemd service (starts at boot)
#
# A reboot is needed after the first run (step 3).
# =============================================================================

# Stop at the first error, on unset variables, and on errors inside pipes.
set -euo pipefail

# Folder of this script, and the pi/ folder above it.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PI_DIR="$(dirname "${SCRIPT_DIR}")"

# Where the LED library is cloned.
RGB_LIB_DISTRIBUTION="${HOME}/rpi-rgb-led-matrix"

# Where buridan_server, protocols and arena_config.json are installed.
INSTALL_PREFIX="${HOME}/buridan"

# Boot configuration file (Raspberry Pi OS Bookworm and later).
BOOT_CONFIG="/boot/firmware/config.txt"
if [ ! -f "${BOOT_CONFIG}" ]; then
  # Older Raspberry Pi OS releases.
  BOOT_CONFIG="/boot/config.txt"
fi

echo "=== 1/4 Installing packages ==="
sudo apt-get update
sudo apt-get install -y git build-essential libzmq3-dev cppzmq-dev nlohmann-json3-dev python3-zmq

echo "=== 2/4 Building rpi-rgb-led-matrix ==="
if [ ! -d "${RGB_LIB_DISTRIBUTION}" ]; then
  git clone https://github.com/hzeller/rpi-rgb-led-matrix.git "${RGB_LIB_DISTRIBUTION}"
fi
make -C "${RGB_LIB_DISTRIBUTION}/lib"

echo "=== 3/4 Disabling on-board audio (conflicts with the LED driver) ==="
# Turn off the audio device tree parameter.
if grep -q "^dtparam=audio=on" "${BOOT_CONFIG}"; then
  sudo sed -i 's/^dtparam=audio=on/dtparam=audio=off/' "${BOOT_CONFIG}"
  echo "set dtparam=audio=off in ${BOOT_CONFIG}"
fi
# Prevent the sound kernel module from being loaded.
BLACKLIST_FILE="/etc/modprobe.d/blacklist-rgb-matrix.conf"
if [ ! -f "${BLACKLIST_FILE}" ]; then
  echo "blacklist snd_bcm2835" | sudo tee "${BLACKLIST_FILE}" > /dev/null
  sudo update-initramfs -u
  echo "created ${BLACKLIST_FILE}"
fi

echo "=== 4/5 Building and installing buridan_server ==="
make -C "${PI_DIR}" RGB_LIB_DISTRIBUTION="${RGB_LIB_DISTRIBUTION}"
make -C "${PI_DIR}" install RGB_LIB_DISTRIBUTION="${RGB_LIB_DISTRIBUTION}" PREFIX="${INSTALL_PREFIX}"

echo "=== 5/5 Installing the buridan-server system service (starts at boot) ==="
SERVICE_FILE="/etc/systemd/system/buridan-server.service"
# Fill in the experiment user and home folder in the service template.
sed -e "s|@USER@|${USER}|g" -e "s|@HOME@|${HOME}|g" "${PI_DIR}/buridan-server.service" | sudo tee "${SERVICE_FILE}" > /dev/null
sudo systemctl daemon-reload
sudo systemctl enable buridan-server
echo "installed ${SERVICE_FILE}"

echo
echo "Done. buridan_server is installed in ${INSTALL_PREFIX}/bin and starts at boot."
echo
echo "If this was the first run, reboot now:   sudo reboot"
echo "To check the panel order, stop the service and run the test pattern by hand:"
echo "  sudo systemctl stop buridan-server"
echo "  sudo ${INSTALL_PREFIX}/bin/buridan_server --test-pattern --led-show-refresh"
echo "Edit ${INSTALL_PREFIX}/arena_config.json if needed (see pi/src/TestPattern.h), then:"
echo "  sudo systemctl start buridan-server"
