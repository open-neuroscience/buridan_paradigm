// =============================================================================
// TestPattern.cpp
//
// Implementation of the panel identification pattern. See TestPattern.h.
// =============================================================================
#include "TestPattern.h"

#include <array>

namespace buridan {

namespace {

// Side length of the identification squares, in pixels.
constexpr int kSquareSize = 6;

// Gap between neighbouring squares, in pixels.
constexpr int kSquareGap = 3;

// Distance of the first square from the panel edges, in pixels.
constexpr int kSquareMargin = 4;

// Builds an Rgb value from three channel values.
Rgb makeRgb(uint8_t red, uint8_t green, uint8_t blue) {
  Rgb colour;
  colour.red = red;
  colour.green = green;
  colour.blue = blue;
  return colour;
}

// Fills a rectangle of the frame (inclusive start, exclusive end).
void fillRectangle(RingFrame* frame, int xStart, int yStart, int width, int height, const Rgb& colour) {
  for (int y = yStart; y < yStart + height; ++y) {
    for (int x = xStart; x < xStart + width; ++x) {
      frame->setPixel(x, y, colour);
    }
  }
}

}  // namespace

void drawTestPattern(RingFrame* frame) {
  // One dim background colour per panel, so the panels are easy to tell apart.
  const std::array<Rgb, kPanelCount> backgrounds = {
      makeRgb(96, 0, 0),    // panel 1: red
      makeRgb(0, 96, 0),    // panel 2: green
      makeRgb(0, 0, 96),    // panel 3: blue
      makeRgb(96, 96, 0),   // panel 4: yellow
      makeRgb(0, 96, 96),   // panel 5: cyan
      makeRgb(96, 0, 96)};  // panel 6: magenta
  const Rgb white = makeRgb(255, 255, 255);

  for (int ringPanel = 0; ringPanel < kPanelCount; ++ringPanel) {
    const int panelLeftX = ringPanel * kPanelCols;

    // Background of the whole panel.
    fillRectangle(frame, panelLeftX, 0, kPanelCols, kPanelRows, backgrounds[ringPanel]);

    // White line along the left edge of the panel.
    fillRectangle(frame, panelLeftX, 0, 1, kPanelRows, white);

    // (panel number) squares along the top edge, starting from the left.
    const int panelNumber = ringPanel + 1;
    for (int square = 0; square < panelNumber; ++square) {
      const int squareX = panelLeftX + kSquareMargin + square * (kSquareSize + kSquareGap);
      const int squareY = kSquareMargin;
      fillRectangle(frame, squareX, squareY, kSquareSize, kSquareSize, white);
    }
  }
}

}  // namespace buridan
