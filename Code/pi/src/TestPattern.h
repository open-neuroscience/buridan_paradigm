// =============================================================================
// TestPattern.h
//
// A static pattern used to check the panel order and orientation.
//
// Each ring panel k (0..5) is drawn as follows:
//   - background in a distinct colour:
//       panel 1 red, 2 green, 3 blue, 4 yellow, 5 cyan, 6 magenta
//   - (k + 1) white squares along the TOP edge, starting at the panel's LEFT
//     edge (seen from inside the arena). The number of squares is the panel
//     number in ring order.
//   - a white line along the panel's left edge.
//
// How to read it:
//   - squares at the bottom instead of the top  -> set panel_flip_y for that panel
//   - squares starting at the right             -> set panel_flip_x for that panel
//   - numbers not increasing 1..6 around the ring -> fix panel_order
//   - after fixing the order, note whether numbers increase clockwise when seen
//     from above, and set x_increases_clockwise accordingly.
// =============================================================================
#pragma once

#include "RingFrame.h"

namespace buridan {

// Draws the test pattern into the frame.
void drawTestPattern(RingFrame* frame);

}  // namespace buridan
