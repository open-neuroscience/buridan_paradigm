// =============================================================================
// RingMapper.cpp
//
// Implementation of the ring-to-canvas mapping. See RingMapper.h.
// =============================================================================
#include "RingMapper.h"

#include <canvas.h>

namespace buridan {

RingMapper::RingMapper(const ArenaConfig& config) {
  lookup_.resize(static_cast<std::size_t>(kRingWidth) * kRingHeight);

  for (int ringY = 0; ringY < kRingHeight; ++ringY) {
    for (int ringX = 0; ringX < kRingWidth; ++ringX) {
      // Which 64-pixel block of the ring this pixel belongs to.
      const int ringPanel = ringX / kPanelCols;

      // Position of the pixel inside its panel, before any flipping.
      int localX = ringX % kPanelCols;
      int localY = ringY;

      // Correct for panels that are mounted mirrored or upside down.
      if (config.panelFlipX[ringPanel]) {
        localX = (kPanelCols - 1) - localX;
      }
      if (config.panelFlipY[ringPanel]) {
        localY = (kPanelRows - 1) - localY;
      }

      // Which physical output slot drives this ring panel.
      const int slot = config.panelOrder[ringPanel];

      // Decompose the slot into chain number and position along the chain.
      const int chain = slot / kPanelsPerChain;
      const int positionInChain = slot % kPanelsPerChain;

      // Translate into library canvas coordinates.
      CanvasPoint point;
      point.x = positionInChain * kPanelCols + localX;
      point.y = chain * kPanelRows + localY;

      const std::size_t lookupIndex = static_cast<std::size_t>(ringY) * kRingWidth + ringX;
      lookup_[lookupIndex] = point;
    }
  }
}

void RingMapper::draw(const RingFrame& frame, rgb_matrix::Canvas* canvas) const {
  const std::vector<uint8_t>& bytes = frame.bytes();

  for (std::size_t pixelIndex = 0; pixelIndex < lookup_.size(); ++pixelIndex) {
    const CanvasPoint& point = lookup_[pixelIndex];
    const std::size_t byteIndex = pixelIndex * kChannelsPerPixel;
    const uint8_t red = bytes[byteIndex + 0];
    const uint8_t green = bytes[byteIndex + 1];
    const uint8_t blue = bytes[byteIndex + 2];
    canvas->SetPixel(point.x, point.y, red, green, blue);
  }
}

}  // namespace buridan
