// =============================================================================
// Stimulus.cpp
//
// Stimulus factory and shared parameter-reading helpers. See Stimulus.h.
// =============================================================================
#include "Stimulus.h"

#include <cmath>
#include <stdexcept>

#include "BarRegionsStimulus.h"
#include "GratingStimulus.h"
#include "StripesStimulus.h"

namespace buridan {

std::unique_ptr<Stimulus> createStimulus(const std::string& type,
                                         const nlohmann::json& params,
                                         const ArenaConfig& arenaConfig) {
  if (type == "stripes") {
    return std::make_unique<StripesStimulus>(params);
  }
  if (type == "grating") {
    return std::make_unique<GratingStimulus>(params, arenaConfig);
  }
  if (type == "bar_regions") {
    return std::make_unique<BarRegionsStimulus>(params);
  }
  if (type == "blank") {
    return std::make_unique<BlankStimulus>(params);
  }
  throw std::invalid_argument("unknown stimulus type: '" + type + "'");
}

Rgb readColour(const nlohmann::json& params, const std::string& key, const Rgb& defaultColour) {
  if (!params.contains(key)) {
    return defaultColour;
  }
  const nlohmann::json& value = params.at(key);
  if (!value.is_array() || value.size() != 3) {
    throw std::invalid_argument("'" + key + "' must be an array [r, g, b]");
  }

  // Read and range-check each channel explicitly.
  int channels[3] = {0, 0, 0};
  for (int channel = 0; channel < 3; ++channel) {
    const nlohmann::json& element = value.at(channel);
    if (!element.is_number_integer()) {
      throw std::invalid_argument("'" + key + "' values must be integers 0..255");
    }
    const int channelValue = element.get<int>();
    if (channelValue < 0 || channelValue > 255) {
      throw std::invalid_argument("'" + key + "' values must be integers 0..255");
    }
    channels[channel] = channelValue;
  }

  Rgb colour;
  colour.red = static_cast<uint8_t>(channels[0]);
  colour.green = static_cast<uint8_t>(channels[1]);
  colour.blue = static_cast<uint8_t>(channels[2]);
  return colour;
}

double readNumber(const nlohmann::json& params, const std::string& key, double defaultValue) {
  if (!params.contains(key)) {
    return defaultValue;
  }
  const nlohmann::json& value = params.at(key);
  if (!value.is_number()) {
    throw std::invalid_argument("'" + key + "' must be a number");
  }
  return value.get<double>();
}

std::string readString(const nlohmann::json& params, const std::string& key, const std::string& defaultValue) {
  if (!params.contains(key)) {
    return defaultValue;
  }
  const nlohmann::json& value = params.at(key);
  if (!value.is_string()) {
    throw std::invalid_argument("'" + key + "' must be a string");
  }
  return value.get<std::string>();
}

double columnCentreDegrees(int x) {
  const double columnCentre = static_cast<double>(x) + 0.5;
  return columnCentre * kDegreesPerPixel;
}

double wrapDegrees(double angle) {
  double wrapped = std::fmod(angle, kDegreesPerRing);
  // std::fmod keeps the sign of the input, so shift negative results up.
  if (wrapped < 0.0) {
    wrapped += kDegreesPerRing;
  }
  return wrapped;
}

BlankStimulus::BlankStimulus(const nlohmann::json& params) {
  const Rgb black;
  colour_ = readColour(params, "rgb", black);
}

void BlankStimulus::render(RingFrame* frame, double /*secondsSinceEpochStart*/) {
  frame->fill(colour_);
}

}  // namespace buridan
