// =============================================================================
// RingFrame.cpp
//
// Implementation of the RingFrame RGB buffer. See RingFrame.h for the layout.
// =============================================================================
#include "RingFrame.h"

#include <algorithm>

namespace buridan {

RingFrame::RingFrame() {
  // Allocate the buffer and initialise every byte to 0 (black).
  bytes_.assign(kRingFrameBytes, 0);
}

void RingFrame::fill(const Rgb& colour) {
  // Walk through the buffer one pixel (3 bytes) at a time.
  for (std::size_t index = 0; index < bytes_.size(); index += kChannelsPerPixel) {
    bytes_[index + 0] = colour.red;
    bytes_[index + 1] = colour.green;
    bytes_[index + 2] = colour.blue;
  }
}

void RingFrame::setPixel(int x, int y, const Rgb& colour) {
  // Silently ignore pixels that fall outside the ring.
  if (!isInside(x, y)) {
    return;
  }
  const std::size_t index = byteIndex(x, y);
  bytes_[index + 0] = colour.red;
  bytes_[index + 1] = colour.green;
  bytes_[index + 2] = colour.blue;
}

Rgb RingFrame::getPixel(int x, int y) const {
  Rgb colour;
  // Pixels outside the ring are reported as black.
  if (!isInside(x, y)) {
    return colour;
  }
  const std::size_t index = byteIndex(x, y);
  colour.red = bytes_[index + 0];
  colour.green = bytes_[index + 1];
  colour.blue = bytes_[index + 2];
  return colour;
}

bool RingFrame::copyFromPackedRgb(const uint8_t* data, std::size_t size) {
  // Reject buffers that do not describe exactly one full ring frame.
  if (size != static_cast<std::size_t>(kRingFrameBytes)) {
    return false;
  }
  std::copy(data, data + size, bytes_.begin());
  return true;
}

const std::vector<uint8_t>& RingFrame::bytes() const {
  return bytes_;
}

bool RingFrame::isInside(int x, int y) {
  const bool xInside = (x >= 0) && (x < kRingWidth);
  const bool yInside = (y >= 0) && (y < kRingHeight);
  return xInside && yInside;
}

std::size_t RingFrame::byteIndex(int x, int y) {
  const std::size_t pixelIndex = static_cast<std::size_t>(y) * kRingWidth + static_cast<std::size_t>(x);
  return pixelIndex * kChannelsPerPixel;
}

}  // namespace buridan
