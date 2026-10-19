// =============================================================================
// RingMapper.h
//
// Copies a RingFrame (384 x 64 ring coordinates) onto the rpi-rgb-led-matrix
// canvas.
//
// How the library lays out our panels:
//   With --led-chain=2 and --led-parallel=3 the library canvas is
//     width  = cols * chain    = 64 * 2 = 128 pixels
//     height = rows * parallel = 64 * 3 = 192 pixels
//   Each parallel chain is a horizontal band 64 pixels high:
//     canvas y   0..63  -> chain 1 (panels 1 and 2)
//     canvas y  64..127 -> chain 2 (panels 3 and 4)
//     canvas y 128..191 -> chain 3 (panels 5 and 6)
//   Within a band, the first panel of the chain is canvas x 0..63 and the
//   second panel is canvas x 64..127.
//
// For speed, the mapping of every ring pixel to a canvas pixel is computed
// once in the constructor and stored in a lookup table.
// =============================================================================
#pragma once

#include <vector>

#include "ArenaConfig.h"
#include "RingFrame.h"

namespace rgb_matrix {
class Canvas;
}

namespace buridan {

// Width of the library canvas for our wiring (128 pixels).
constexpr int kCanvasWidth = kPanelCols * kPanelsPerChain;

// Height of the library canvas for our wiring (192 pixels).
constexpr int kCanvasHeight = kPanelRows * kParallelChains;

class RingMapper {
 public:
  // Builds the lookup table from the arena configuration.
  explicit RingMapper(const ArenaConfig& config);

  // Draws every pixel of the ring frame onto the canvas.
  void draw(const RingFrame& frame, rgb_matrix::Canvas* canvas) const;

 private:
  // Canvas position of one ring pixel.
  struct CanvasPoint {
    int x = 0;
    int y = 0;
  };

  // lookup_[y * kRingWidth + x] = canvas position of ring pixel (x, y).
  std::vector<CanvasPoint> lookup_;
};

}  // namespace buridan
