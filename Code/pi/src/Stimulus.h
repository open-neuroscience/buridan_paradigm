// =============================================================================
// Stimulus.h
//
// Common interface for every visual stimulus that the Pi can render itself.
//
// A stimulus is created from the "params" object of a protocol epoch, and is
// then asked once per display frame to draw itself into a RingFrame for the
// time elapsed since the start of the epoch.
//
// Angles: protocols describe positions and sizes in degrees of the ring.
//   0 deg is the left edge of ring pixel column 0, and angles grow in the
//   direction of increasing ring x (360 deg = 384 pixels).
//   Directions of motion are given as "cw" / "ccw" (seen from above) and are
//   translated into pixel directions using ArenaConfig::xIncreasesClockwise.
// =============================================================================
#pragma once

#include <memory>
#include <string>

#include <nlohmann/json.hpp>

#include "ArenaConfig.h"
#include "RingFrame.h"

namespace buridan {

class Stimulus {
 public:
  virtual ~Stimulus() = default;

  // Draws the stimulus into the frame.
  // secondsSinceEpochStart: time elapsed since this stimulus started showing.
  virtual void render(RingFrame* frame, double secondsSinceEpochStart) = 0;
};

// Creates the stimulus described by an epoch "type" and its "params" object.
// Known types: "stripes", "grating", "bar_regions", "blank".
// Throws std::invalid_argument with a readable message if the type is unknown
// or a parameter is invalid.
std::unique_ptr<Stimulus> createStimulus(const std::string& type,
                                         const nlohmann::json& params,
                                         const ArenaConfig& arenaConfig);

// ----------------------------------------------------------------------------
// Helpers shared by stimulus implementations.
// ----------------------------------------------------------------------------

// Reads an [r, g, b] array (values 0..255) from params[key].
// Returns defaultColour if the key is missing.
// Throws std::invalid_argument if the value is not a valid colour.
Rgb readColour(const nlohmann::json& params, const std::string& key, const Rgb& defaultColour);

// Reads a number from params[key], or returns defaultValue if the key is missing.
// Throws std::invalid_argument if the value is not a number.
double readNumber(const nlohmann::json& params, const std::string& key, double defaultValue);

// Reads a string from params[key], or returns defaultValue if the key is missing.
// Throws std::invalid_argument if the value is not a string.
std::string readString(const nlohmann::json& params, const std::string& key, const std::string& defaultValue);

// Returns the angle (degrees) of the centre of ring pixel column x.
double columnCentreDegrees(int x);

// Wraps an angle into the range [0, 360).
double wrapDegrees(double angle);

// A stimulus that shows a single uniform colour (default black).
// Params: "rgb": [r, g, b]
class BlankStimulus : public Stimulus {
 public:
  explicit BlankStimulus(const nlohmann::json& params);
  void render(RingFrame* frame, double secondsSinceEpochStart) override;

 private:
  Rgb colour_;
};

}  // namespace buridan
