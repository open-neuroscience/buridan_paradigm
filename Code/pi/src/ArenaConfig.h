// =============================================================================
// ArenaConfig.h
//
// Physical configuration of the arena that cannot be known in software:
//   - which hardware panel slot shows which part of the ring,
//   - whether a panel is mounted mirrored / upside down,
//   - in which direction ring x increases when seen from above.
//
// The configuration is read from a JSON file (default: arena_config.json).
// Use the --test-pattern mode of buridan_server to work out the right values.
//
// Example file:
// {
//   "panel_order": [0, 1, 2, 3, 4, 5],
//   "panel_flip_x": [false, false, false, false, false, false],
//   "panel_flip_y": [false, false, false, false, false, false],
//   "x_increases_clockwise": true
// }
//
// Definitions:
//   - "ring panel" k (0..5) is the k-th 64-pixel block of the ring, going
//     around in the direction of increasing ring x.
//   - "hardware slot" s (0..5) is the physical output position:
//       chain       = s / 2   (0 = chain 1, 1 = chain 2, 2 = chain 3)
//       position    = s % 2   (0 = first panel on that chain, 1 = second)
//     So slot 0 = README panel 1, slot 1 = panel 2, ..., slot 5 = panel 6.
//   - panel_order[k] = hardware slot that displays ring panel k.
// =============================================================================
#pragma once

#include <array>
#include <string>

#include "RingGeometry.h"

namespace buridan {

struct ArenaConfig {
  // panel_order[k] = hardware slot (0..5) showing ring panel k.
  std::array<int, kPanelCount> panelOrder = {0, 1, 2, 3, 4, 5};

  // panelFlipX[k] = true mirrors ring panel k left/right.
  std::array<bool, kPanelCount> panelFlipX = {false, false, false, false, false, false};

  // panelFlipY[k] = true mirrors ring panel k top/bottom.
  std::array<bool, kPanelCount> panelFlipY = {false, false, false, false, false, false};

  // true if, seen from above, increasing ring x moves clockwise.
  // Used to translate "cw" / "ccw" in protocols into a pixel direction.
  bool xIncreasesClockwise = true;
};

// Loads an ArenaConfig from a JSON file.
// Keys that are missing keep their default value.
// On failure, returns false and writes a human-readable reason into errorMessage.
bool loadArenaConfig(const std::string& path, ArenaConfig* config, std::string* errorMessage);

// Checks that panel_order is a permutation of 0..5.
// On failure, returns false and writes the reason into errorMessage.
bool validateArenaConfig(const ArenaConfig& config, std::string* errorMessage);

}  // namespace buridan
