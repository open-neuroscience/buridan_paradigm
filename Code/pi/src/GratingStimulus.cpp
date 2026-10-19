// =============================================================================
// GratingStimulus.cpp
//
// Implementation of the rotating grating stimulus. See GratingStimulus.h.
// =============================================================================
#include "GratingStimulus.h"

#include <cmath>
#include <stdexcept>

namespace buridan {

namespace {

// Pi, written out explicitly to avoid relying on non-standard M_PI.
constexpr double kPi = 3.14159265358979323846;

// Linear interpolation of one 8-bit channel: weight 1 gives a, weight 0 gives b.
uint8_t mixChannel(uint8_t a, uint8_t b, double weightA) {
  const double mixed = weightA * static_cast<double>(a) + (1.0 - weightA) * static_cast<double>(b);
  const double rounded = std::round(mixed);
  return static_cast<uint8_t>(rounded);
}

}  // namespace

GratingStimulus::GratingStimulus(const nlohmann::json& params, const ArenaConfig& arenaConfig) {
  periodDeg_ = readNumber(params, "period_deg", 30.0);
  if (periodDeg_ <= 0.0) {
    throw std::invalid_argument("'period_deg' must be greater than 0");
  }

  speedDegPerSecond_ = readNumber(params, "speed_deg_s", 0.0);
  if (speedDegPerSecond_ < 0.0) {
    throw std::invalid_argument("'speed_deg_s' must be 0 or positive (use 'direction' to reverse)");
  }

  // Translate the clockwise / counter-clockwise direction into a pixel direction.
  const std::string direction = readString(params, "direction", "cw");
  bool movesClockwise = true;
  if (direction == "cw") {
    movesClockwise = true;
  } else if (direction == "ccw") {
    movesClockwise = false;
  } else {
    throw std::invalid_argument("'direction' must be \"cw\" or \"ccw\"");
  }
  const bool movesTowardsIncreasingX = (movesClockwise == arenaConfig.xIncreasesClockwise);
  if (movesTowardsIncreasingX) {
    pixelDirectionSign_ = 1.0;
  } else {
    pixelDirectionSign_ = -1.0;
  }

  const std::string waveform = readString(params, "waveform", "square");
  if (waveform == "square") {
    isSine_ = false;
  } else if (waveform == "sine") {
    isSine_ = true;
  } else {
    throw std::invalid_argument("'waveform' must be \"square\" or \"sine\"");
  }

  dutyCycle_ = readNumber(params, "duty_cycle", 0.5);
  if (dutyCycle_ < 0.0 || dutyCycle_ > 1.0) {
    throw std::invalid_argument("'duty_cycle' must be between 0 and 1");
  }

  phaseDeg_ = readNumber(params, "phase_deg", 0.0);

  Rgb white;
  white.red = 255;
  white.green = 255;
  white.blue = 255;
  Rgb black;
  colourA_ = readColour(params, "rgb_a", white);
  colourB_ = readColour(params, "rgb_b", black);
}

Rgb GratingStimulus::colourAtPatternDegrees(double patternDeg) const {
  // Position inside one period, in the range [0, 1).
  double fraction = std::fmod(patternDeg, periodDeg_) / periodDeg_;
  if (fraction < 0.0) {
    fraction += 1.0;
  }

  if (!isSine_) {
    // Square wave: colour A for the first duty_cycle of the period, else B.
    if (fraction < dutyCycle_) {
      return colourA_;
    }
    return colourB_;
  }

  // Sine wave: weight goes smoothly from 1 (colour A) to 0 (colour B) and back.
  const double weightA = 0.5 + 0.5 * std::cos(2.0 * kPi * fraction);
  Rgb colour;
  colour.red = mixChannel(colourA_.red, colourB_.red, weightA);
  colour.green = mixChannel(colourA_.green, colourB_.green, weightA);
  colour.blue = mixChannel(colourA_.blue, colourB_.blue, weightA);
  return colour;
}

void GratingStimulus::render(RingFrame* frame, double secondsSinceEpochStart) {
  // How far the pattern has moved since the epoch started, in pixel direction.
  const double displacementDeg = pixelDirectionSign_ * speedDegPerSecond_ * secondsSinceEpochStart;

  for (int x = 0; x < kRingWidth; ++x) {
    // Moving the pattern forward by d is the same as sampling it at (angle - d).
    const double columnDeg = columnCentreDegrees(x);
    const double patternDeg = columnDeg - displacementDeg - phaseDeg_;
    const Rgb colour = colourAtPatternDegrees(patternDeg);

    // Gratings are vertical: the whole column has one colour.
    for (int y = 0; y < kRingHeight; ++y) {
      frame->setPixel(x, y, colour);
    }
  }
}

}  // namespace buridan
