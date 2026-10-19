// =============================================================================
// BarRegionsStimulus.cpp
//
// Implementation of the two-region bar stimulus. See BarRegionsStimulus.h.
// =============================================================================
#include "BarRegionsStimulus.h"

#include <cmath>
#include <stdexcept>

namespace buridan {

namespace {

// Allowance for rounding errors when comparing computed widths with limits,
// so that a bar of exactly one pixel is not rejected.
constexpr double kWidthToleranceDeg = 1e-9;

// Default layout of each region: 90 degrees wide, 3 bars, bar fraction 0.25
// (10 degree bars with 30 degree gaps).
constexpr double kDefaultRegionWidthDeg = 90.0;
constexpr int kDefaultBarCount = 3;
constexpr double kDefaultBarFraction = 0.25;

// Reads a whole number from params[key], or returns defaultValue if the key
// is missing. Throws std::invalid_argument if the value is not an integer.
int readInteger(const nlohmann::json& params, const std::string& key, int defaultValue) {
  if (!params.contains(key)) {
    return defaultValue;
  }
  const nlohmann::json& value = params.at(key);
  if (!value.is_number_integer()) {
    throw std::invalid_argument("'" + key + "' must be a whole number");
  }
  return value.get<int>();
}

}  // namespace

BarRegionsStimulus::BarRegionsStimulus(const nlohmann::json& params) {
  // Region A is centred on center_deg, region B exactly opposite.
  const double centerDeg = readNumber(params, "center_deg", 0.0);
  const double regionACenterDeg = wrapDegrees(centerDeg);
  const double regionBCenterDeg = wrapDegrees(centerDeg + kDegreesPerRing / 2.0);
  regionA_ = readRegion(params, "region_a", regionACenterDeg);
  regionB_ = readRegion(params, "region_b", regionBCenterDeg);

  // The regions are centred 180 degrees apart, so they overlap only if their
  // half-widths add up to more than 180 degrees.
  const double totalWidthDeg = regionA_.widthDeg + regionB_.widthDeg;
  if (totalWidthDeg > kDegreesPerRing + kWidthToleranceDeg) {
    throw std::invalid_argument("'region_a_width_deg' + 'region_b_width_deg' must be at most 360 (regions overlap)");
  }

  // Colours: default dark bars on a bright background, as for the stripes.
  Rgb black;
  Rgb white;
  white.red = 255;
  white.green = 255;
  white.blue = 255;
  barColour_ = readColour(params, "bar_rgb", black);
  backgroundColour_ = readColour(params, "background_rgb", white);

  // The bars never move: decide once which columns belong to a bar.
  for (int x = 0; x < kRingWidth; ++x) {
    const double columnDeg = columnCentreDegrees(x);
    const bool onBarA = isAngleOnBar(columnDeg, regionA_);
    const bool onBarB = isAngleOnBar(columnDeg, regionB_);
    columnIsBar_[x] = onBarA || onBarB;
  }
}

BarRegion BarRegionsStimulus::readRegion(const nlohmann::json& params,
                                         const std::string& prefix,
                                         double regionCenterDeg) {
  const std::string widthKey = prefix + "_width_deg";
  const std::string barsKey = prefix + "_bars";
  const std::string fractionKey = prefix + "_bar_fraction";

  BarRegion region;
  region.centerDeg = regionCenterDeg;
  region.widthDeg = readNumber(params, widthKey, kDefaultRegionWidthDeg);
  region.barCount = readInteger(params, barsKey, kDefaultBarCount);
  region.barFraction = readNumber(params, fractionKey, kDefaultBarFraction);

  if (region.widthDeg <= 0.0 || region.widthDeg > kDegreesPerRing) {
    throw std::invalid_argument("'" + widthKey + "' must be greater than 0 and at most 360");
  }
  if (region.barCount < 0) {
    throw std::invalid_argument("'" + barsKey + "' must be 0 or more");
  }
  if (region.barFraction <= 0.0 || region.barFraction > 1.0) {
    throw std::invalid_argument("'" + fractionKey + "' must be greater than 0 and at most 1");
  }

  // An empty region shows only the background: no layout to compute.
  if (region.barCount == 0) {
    return region;
  }

  // Bars touch both region edges: N bars and N - 1 gaps fill the width.
  //   N * f * p + (N - 1) * (1 - f) * p = W   ->   p = W / (N - 1 + f)
  const double barCount = static_cast<double>(region.barCount);
  region.pitchDeg = region.widthDeg / (barCount - 1.0 + region.barFraction);
  region.barWidthDeg = region.barFraction * region.pitchDeg;
  const double gapWidthDeg = region.pitchDeg - region.barWidthDeg;

  // Bars or gaps narrower than one pixel cannot be shown reliably.
  if (region.barWidthDeg + kWidthToleranceDeg < kDegreesPerPixel) {
    throw std::invalid_argument(prefix + ": bars would be " + std::to_string(region.barWidthDeg) +
                                " deg wide, less than one pixel (0.9375 deg); use fewer bars, a larger "
                                "bar fraction or a wider region");
  }
  const bool hasGaps = (region.barCount > 1) && (region.barFraction < 1.0);
  if (hasGaps && gapWidthDeg + kWidthToleranceDeg < kDegreesPerPixel) {
    throw std::invalid_argument(prefix + ": gaps between bars would be " + std::to_string(gapWidthDeg) +
                                " deg wide, less than one pixel (0.9375 deg); use fewer bars, a smaller "
                                "bar fraction or a wider region");
  }
  return region;
}

bool BarRegionsStimulus::isAngleOnBar(double angleDeg, const BarRegion& region) {
  if (region.barCount == 0) {
    return false;
  }

  // Position of the angle measured from the left edge of the region,
  // taking the wrap-around at 360 degrees into account.
  const double regionStartDeg = region.centerDeg - region.widthDeg / 2.0;
  const double offsetDeg = wrapDegrees(angleDeg - regionStartDeg);
  if (offsetDeg > region.widthDeg) {
    return false;
  }

  // Which bar's pitch interval the angle falls in. The right edge of the
  // region belongs to the last bar.
  int barIndex = static_cast<int>(std::floor(offsetDeg / region.pitchDeg));
  if (barIndex > region.barCount - 1) {
    barIndex = region.barCount - 1;
  }
  const double barStartDeg = static_cast<double>(barIndex) * region.pitchDeg;
  const double barEndDeg = barStartDeg + region.barWidthDeg;
  const bool onBar = (offsetDeg >= barStartDeg) && (offsetDeg <= barEndDeg);
  return onBar;
}

void BarRegionsStimulus::render(RingFrame* frame, double /*secondsSinceEpochStart*/) {
  // The bars are static, so the elapsed time is not used.
  frame->fill(backgroundColour_);

  for (int x = 0; x < kRingWidth; ++x) {
    if (!columnIsBar_[x]) {
      continue;
    }
    // Bars cover the full height, top to bottom.
    for (int y = 0; y < kRingHeight; ++y) {
      frame->setPixel(x, y, barColour_);
    }
  }
}

}  // namespace buridan
