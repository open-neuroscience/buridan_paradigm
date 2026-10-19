// =============================================================================
// ArenaConfig.cpp
//
// Loading and validation of the arena configuration file. See ArenaConfig.h.
// =============================================================================
#include "ArenaConfig.h"

#include <fstream>

#include <nlohmann/json.hpp>

namespace buridan {

namespace {

// Reads a JSON array of exactly kPanelCount elements into a std::array.
// Returns false (with an error message) if the array has the wrong length.
template <typename T>
bool readPanelArray(const nlohmann::json& document,
                    const std::string& key,
                    std::array<T, kPanelCount>* target,
                    std::string* errorMessage) {
  // A missing key is not an error: the default value is kept.
  if (!document.contains(key)) {
    return true;
  }
  const nlohmann::json& values = document.at(key);
  if (!values.is_array()) {
    *errorMessage = "'" + key + "' must be an array";
    return false;
  }
  if (values.size() != static_cast<std::size_t>(kPanelCount)) {
    *errorMessage = "'" + key + "' must have exactly 6 elements";
    return false;
  }
  for (int index = 0; index < kPanelCount; ++index) {
    (*target)[index] = values.at(index).get<T>();
  }
  return true;
}

}  // namespace

bool loadArenaConfig(const std::string& path, ArenaConfig* config, std::string* errorMessage) {
  std::ifstream file(path);
  if (!file.is_open()) {
    *errorMessage = "cannot open arena config file: " + path;
    return false;
  }

  // Parse the file. Any syntax or type error is reported as a failure.
  try {
    nlohmann::json document = nlohmann::json::parse(file);

    if (!readPanelArray(document, "panel_order", &config->panelOrder, errorMessage)) {
      return false;
    }
    if (!readPanelArray(document, "panel_flip_x", &config->panelFlipX, errorMessage)) {
      return false;
    }
    if (!readPanelArray(document, "panel_flip_y", &config->panelFlipY, errorMessage)) {
      return false;
    }
    if (document.contains("x_increases_clockwise")) {
      config->xIncreasesClockwise = document.at("x_increases_clockwise").get<bool>();
    }
  } catch (const nlohmann::json::exception& error) {
    *errorMessage = std::string("invalid arena config: ") + error.what();
    return false;
  }

  return validateArenaConfig(*config, errorMessage);
}

bool validateArenaConfig(const ArenaConfig& config, std::string* errorMessage) {
  // Every hardware slot must be used exactly once.
  std::array<bool, kPanelCount> slotUsed = {false, false, false, false, false, false};
  for (int ringPanel = 0; ringPanel < kPanelCount; ++ringPanel) {
    const int slot = config.panelOrder[ringPanel];
    if (slot < 0 || slot >= kPanelCount) {
      *errorMessage = "panel_order values must be between 0 and 5";
      return false;
    }
    if (slotUsed[slot]) {
      *errorMessage = "panel_order must use each slot 0..5 exactly once";
      return false;
    }
    slotUsed[slot] = true;
  }
  return true;
}

}  // namespace buridan
