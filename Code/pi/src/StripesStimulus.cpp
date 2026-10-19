// =============================================================================
// StripesStimulus.cpp
//
// Implementation of the Buridan stripes stimulus. See StripesStimulus.h.
// =============================================================================
#include "StripesStimulus.h"

#include <cmath>
#include <stdexcept>

namespace buridan {

StripesStimulus::StripesStimulus(const nlohmann::json& params) {
  // Stripe centres: default is the classic pair, 180 degrees apart.
  if (params.contains("centers_deg")) {
    const nlohmann::json& centers = params.at("centers_deg");
    if (!centers.is_array()) {
      throw std::invalid_argument("'centers_deg' must be an array of numbers");
    }
    for (const nlohmann::json& center : centers) {
      if (!center.is_number()) {
        throw std::invalid_argument("'centers_deg' must be an array of numbers");
      }
      const double centerDeg = center.get<double>();
      centersDeg_.push_back(wrapDegrees(centerDeg));
    }
  } else {
    centersDeg_.push_back(0.0);
    centersDeg_.push_back(180.0);
  }

  // Stripe width: default 12 pixels (11.25 degrees).
  widthDeg_ = readNumber(params, "width_deg", 11.25);
  if (widthDeg_ <= 0.0 || widthDeg_ > kDegreesPerRing) {
    throw std::invalid_argument("'width_deg' must be greater than 0 and at most 360");
  }

  // Colours: default dark stripes on a bright background.
  Rgb black;
  Rgb white;
  white.red = 255;
  white.green = 255;
  white.blue = 255;
  stripeColour_ = readColour(params, "stripe_rgb", black);
  backgroundColour_ = readColour(params, "background_rgb", white);

  // Vertical extent of the stripes: default full panel height.
  const double topValue = readNumber(params, "stripe_top_px", 0.0);
  const double bottomValue = readNumber(params, "stripe_bottom_px", static_cast<double>(kRingHeight - 1));
  stripeTopPx_ = static_cast<int>(topValue);
  stripeBottomPx_ = static_cast<int>(bottomValue);
  if (stripeTopPx_ < 0 || stripeBottomPx_ >= kRingHeight || stripeTopPx_ > stripeBottomPx_) {
    throw std::invalid_argument("stripe rows must satisfy 0 <= stripe_top_px <= stripe_bottom_px <= 63");
  }
}

bool StripesStimulus::isColumnInStripe(int x) const {
  const double columnDeg = columnCentreDegrees(x);
  const double halfWidthDeg = widthDeg_ / 2.0;

  for (const double centerDeg : centersDeg_) {
    // Shortest angular distance between the column and the stripe centre,
    // taking the wrap-around at 360 degrees into account.
    const double rawDistance = std::fabs(columnDeg - centerDeg);
    const double wrappedDistance = kDegreesPerRing - rawDistance;
    const double distance = std::fmin(rawDistance, wrappedDistance);
    if (distance <= halfWidthDeg) {
      return true;
    }
  }
  return false;
}

void StripesStimulus::render(RingFrame* frame, double /*secondsSinceEpochStart*/) {
  // The stripes are static, so the elapsed time is not used.
  frame->fill(backgroundColour_);

  for (int x = 0; x < kRingWidth; ++x) {
    if (!isColumnInStripe(x)) {
      continue;
    }
    for (int y = stripeTopPx_; y <= stripeBottomPx_; ++y) {
      frame->setPixel(x, y, stripeColour_);
    }
  }
}

}  // namespace buridan
