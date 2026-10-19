// =============================================================================
// StripesStimulus.h
//
// The classic Buridan stimulus: one or more vertical stripes on a uniform
// background. By default, two dark stripes placed 180 deg apart.
//
// Protocol params (all optional):
//   "centers_deg"      : [0, 180]        stripe centre angles in ring degrees
//   "width_deg"        : 11.25           stripe width in degrees (12 pixels)
//   "stripe_rgb"       : [0, 0, 0]       stripe colour
//   "background_rgb"   : [255, 255, 255] background colour
//   "stripe_top_px"    : 0               first row of the stripe (inclusive)
//   "stripe_bottom_px" : 63              last row of the stripe (inclusive)
// =============================================================================
#pragma once

#include <vector>

#include "Stimulus.h"

namespace buridan {

class StripesStimulus : public Stimulus {
 public:
  // Reads and validates the parameters. Throws std::invalid_argument on error.
  explicit StripesStimulus(const nlohmann::json& params);

  void render(RingFrame* frame, double secondsSinceEpochStart) override;

 private:
  // Returns true if ring column x lies inside any stripe.
  bool isColumnInStripe(int x) const;

  std::vector<double> centersDeg_;
  double widthDeg_ = 0.0;
  Rgb stripeColour_;
  Rgb backgroundColour_;
  int stripeTopPx_ = 0;
  int stripeBottomPx_ = 0;
};

}  // namespace buridan
