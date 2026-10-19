// =============================================================================
// RingFrame.h
//
// A plain RGB image covering the whole LED ring (384 x 64 pixels).
//
// Stimuli draw into a RingFrame without knowing anything about how the panels
// are wired. The RingMapper later copies the RingFrame onto the LED matrix.
//
// Memory layout (packed RGB, row-major, x varies fastest):
//   byte index = (y * kRingWidth + x) * 3 + channel
//   channel 0 = red, 1 = green, 2 = blue
// This is the same layout used by frames streamed from the PC.
// =============================================================================
#pragma once

#include <cstdint>
#include <vector>

#include "RingGeometry.h"

namespace buridan {

// A simple RGB colour with 8 bits per channel.
struct Rgb {
  uint8_t red = 0;
  uint8_t green = 0;
  uint8_t blue = 0;
};

class RingFrame {
 public:
  // Creates a frame with every pixel set to black.
  RingFrame();

  // Sets every pixel of the frame to the given colour.
  void fill(const Rgb& colour);

  // Sets one pixel. Coordinates outside the ring are ignored.
  void setPixel(int x, int y, const Rgb& colour);

  // Returns the colour of one pixel. Coordinates outside the ring return black.
  Rgb getPixel(int x, int y) const;

  // Replaces the whole frame with packed RGB data.
  // Returns false (and leaves the frame unchanged) if the size is not exactly
  // kRingFrameBytes.
  bool copyFromPackedRgb(const uint8_t* data, std::size_t size);

  // Gives read-only access to the raw packed RGB bytes.
  const std::vector<uint8_t>& bytes() const;

 private:
  // Returns true if (x, y) lies inside the ring.
  static bool isInside(int x, int y);

  // Returns the index of the red byte of pixel (x, y).
  static std::size_t byteIndex(int x, int y);

  // Packed RGB pixel data, kRingFrameBytes long.
  std::vector<uint8_t> bytes_;
};

}  // namespace buridan
