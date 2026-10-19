// =============================================================================
// RingGeometry.h
//
// Physical dimensions of the Buridan arena LED ring.
//
// The arena is made of 6 LED panels, each 64 x 64 RGB pixels, placed side by
// side in a ring (1 x 6 arrangement) with all panels facing inward.
//
// Throughout this program we describe stimuli in "ring coordinates":
//   - x runs around the ring, from 0 to kRingWidth - 1 (384 pixels = 360 deg)
//   - y runs from the top row (0) to the bottom row (kRingHeight - 1)
//
// The RingMapper class translates ring coordinates into the coordinate system
// of the rpi-rgb-led-matrix library canvas (see RingMapper.h).
// =============================================================================
#pragma once

namespace buridan {

// Number of pixel rows on a single LED panel.
constexpr int kPanelRows = 64;

// Number of pixel columns on a single LED panel.
constexpr int kPanelCols = 64;

// Total number of panels in the ring.
constexpr int kPanelCount = 6;

// Number of panels daisy-chained on each of the Raspberry Pi output chains.
// Chain 1 drives panels 1 and 2, chain 2 drives panels 3 and 4,
// chain 3 drives panels 5 and 6.
constexpr int kPanelsPerChain = 2;

// Number of parallel output chains used on the Raspberry Pi.
constexpr int kParallelChains = 3;

// Width of the ring in pixels (all panels side by side).
constexpr int kRingWidth = kPanelCols * kPanelCount;  // 384

// Height of the ring in pixels (one panel high).
constexpr int kRingHeight = kPanelRows;  // 64

// Number of colour channels per pixel (red, green, blue).
constexpr int kChannelsPerPixel = 3;

// Size in bytes of one full ring frame in packed RGB format.
// This is also the exact size of a streamed frame received over the network.
constexpr int kRingFrameBytes = kRingWidth * kRingHeight * kChannelsPerPixel;  // 73728

// Full circle in degrees, used to convert between pixels and visual angle.
constexpr double kDegreesPerRing = 360.0;

// Visual angle covered by one pixel column (360 / 384 = 0.9375 deg).
constexpr double kDegreesPerPixel = kDegreesPerRing / static_cast<double>(kRingWidth);

}  // namespace buridan
