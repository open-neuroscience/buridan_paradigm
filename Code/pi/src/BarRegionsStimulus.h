// =============================================================================
// BarRegionsStimulus.h
//
// Two opposing regions of the ring, each filled with evenly spaced vertical
// bars (full height, top to bottom). Region A is centred on "center_deg",
// region B on "center_deg" + 180. Each region has its own width, number of
// bars and bar width, so the two sides can be set independently.
//
// Bar layout inside one region (width W degrees, N bars, bar fraction f):
//   - the first bar starts at the left edge of the region and the last bar
//     ends at its right edge; the gaps between bars are all equal,
//   - pitch (bar centre to bar centre)  p = W / (N - 1 + f)
//   - bar width                         b = f * p
//   - gap between bars                  g = (1 - f) * p
//   so that N * b + (N - 1) * g = W.
//   With N = 1 the single bar fills the whole region; with N = 0 the region
//   shows only the background.
//
//   Example: W = 90, N = 3, f = 0.25  ->  p = 40, b = 10, g = 30
//            |##            ##            ##|
//
// Protocol params (all optional):
//   "center_deg"            : 0                centre of region A, in degrees
//   "region_a_width_deg"    : 90               width of region A
//   "region_a_bars"         : 3                number of bars in region A (0 or more)
//   "region_a_bar_fraction" : 0.25             bar width / pitch in region A (0 < f <= 1)
//   "region_b_width_deg"    : 90               width of region B
//   "region_b_bars"         : 3                number of bars in region B (0 or more)
//   "region_b_bar_fraction" : 0.25             bar width / pitch in region B (0 < f <= 1)
//   "bar_rgb"               : [0, 0, 0]        bar colour
//   "background_rgb"        : [255, 255, 255]  colour everywhere else
//
// Rules: both widths > 0 and together at most 360 (the regions may touch but
// not overlap); every bar, and every gap between bars, at least one pixel
// (0.9375 deg) wide.
// =============================================================================
#pragma once

#include <array>
#include <string>

#include "RingGeometry.h"
#include "Stimulus.h"

namespace buridan {

// The geometry of one region, computed once from the parameters.
struct BarRegion {
  // Centre of the region, in ring degrees [0, 360).
  double centerDeg = 0.0;
  // Width of the region, in degrees.
  double widthDeg = 0.0;
  // Number of bars in the region.
  int barCount = 0;
  // Bar width / pitch.
  double barFraction = 0.0;
  // Distance between the left edges of neighbouring bars, in degrees.
  double pitchDeg = 0.0;
  // Width of one bar, in degrees.
  double barWidthDeg = 0.0;
};

class BarRegionsStimulus : public Stimulus {
 public:
  // Reads and validates the parameters. Throws std::invalid_argument on error.
  explicit BarRegionsStimulus(const nlohmann::json& params);

  void render(RingFrame* frame, double secondsSinceEpochStart) override;

 private:
  // Reads the parameters of one region ("region_a" or "region_b") and
  // computes its bar layout. Throws std::invalid_argument on error.
  static BarRegion readRegion(const nlohmann::json& params,
                              const std::string& prefix,
                              double regionCenterDeg);

  // Returns true if the ring angle 'angleDeg' lies on a bar of 'region'.
  static bool isAngleOnBar(double angleDeg, const BarRegion& region);

  BarRegion regionA_;
  BarRegion regionB_;
  Rgb barColour_;
  Rgb backgroundColour_;
  // Which ring columns show a bar; the stimulus is static, so this is
  // computed once in the constructor.
  std::array<bool, kRingWidth> columnIsBar_{};
};

}  // namespace buridan
