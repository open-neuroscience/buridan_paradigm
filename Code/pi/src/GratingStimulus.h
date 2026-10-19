// =============================================================================
// GratingStimulus.h
//
// A vertical grating that can rotate around the arena (optomotor stimulus).
//
// Protocol params (all optional):
//   "period_deg"   : 30              spatial period (one light + one dark bar)
//   "speed_deg_s"  : 0               rotation speed in degrees per second
//   "direction"    : "cw"            "cw" or "ccw", seen from above
//   "waveform"     : "square"        "square" or "sine"
//   "duty_cycle"   : 0.5             square only: fraction of the period in rgb_a
//   "phase_deg"    : 0               starting offset of the pattern
//   "rgb_a"        : [255, 255, 255] first colour (bright bars)
//   "rgb_b"        : [0, 0, 0]       second colour (dark bars)
// =============================================================================
#pragma once

#include <string>

#include "Stimulus.h"

namespace buridan {

class GratingStimulus : public Stimulus {
 public:
  // Reads and validates the parameters. Throws std::invalid_argument on error.
  GratingStimulus(const nlohmann::json& params, const ArenaConfig& arenaConfig);

  void render(RingFrame* frame, double secondsSinceEpochStart) override;

 private:
  // Returns the colour of a column at the given pattern position.
  Rgb colourAtPatternDegrees(double patternDeg) const;

  double periodDeg_ = 0.0;
  double speedDegPerSecond_ = 0.0;
  // +1 if the pattern moves towards increasing ring x, -1 otherwise.
  double pixelDirectionSign_ = 1.0;
  bool isSine_ = false;
  double dutyCycle_ = 0.5;
  double phaseDeg_ = 0.0;
  Rgb colourA_;
  Rgb colourB_;
};

}  // namespace buridan
