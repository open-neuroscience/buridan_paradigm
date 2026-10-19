// =============================================================================
// Protocol.h
//
// A protocol is a named sequence of epochs. Each epoch shows one stimulus for
// a fixed duration. Protocols are stored as JSON files with the same format on
// the Windows PC (Bonsai) and on the Raspberry Pi.
//
// Example:
// {
//   "name": "stripes_then_grating",
//   "schema_version": 1,
//   "frame_rate_hz": 60,
//   "epochs": [
//     { "type": "stripes", "duration_s": 300, "params": { "width_deg": 11.25 } },
//     { "type": "grating", "duration_s": 60,
//       "params": { "period_deg": 30, "speed_deg_s": 45, "direction": "cw" } }
//   ]
// }
//
// The full schema is documented in docs/protocol-schema.md.
// =============================================================================
#pragma once

#include <string>
#include <vector>

#include <nlohmann/json.hpp>

#include "ArenaConfig.h"

namespace buridan {

// The protocol file format version understood by this program.
constexpr int kProtocolSchemaVersion = 1;

// Frame rate used when a protocol does not specify one.
constexpr double kDefaultProtocolFrameRateHz = 60.0;

// Allowed range for frame_rate_hz.
constexpr double kMinFrameRateHz = 1.0;
constexpr double kMaxFrameRateHz = 240.0;

struct Epoch {
  // Stimulus type, e.g. "stripes", "grating", "bar_regions", "blank".
  std::string type;
  // How long the epoch lasts, in seconds.
  double durationSeconds = 0.0;
  // Stimulus-specific parameters, passed unchanged to the stimulus.
  nlohmann::json params = nlohmann::json::object();
};

struct Protocol {
  std::string name;
  int schemaVersion = kProtocolSchemaVersion;
  double frameRateHz = kDefaultProtocolFrameRateHz;
  std::vector<Epoch> epochs;
};

// Returns true if the name only contains letters, digits, '_' and '-' and is
// 1..64 characters long. This prevents names from escaping the protocol
// folder (for example "../../etc/passwd").
bool isValidProtocolName(const std::string& name);

// Converts JSON into a Protocol and checks every field, including that every
// epoch's stimulus can be created from its params.
// Throws std::invalid_argument with a readable message on any error.
Protocol parseProtocol(const nlohmann::json& document, const ArenaConfig& arenaConfig);

// Converts a Protocol back into JSON (the file format above).
nlohmann::json protocolToJson(const Protocol& protocol);

// Returns the full path of the file for a protocol name inside a folder.
std::string protocolFilePath(const std::string& folder, const std::string& name);

// Reads and parses a protocol file. Throws std::invalid_argument on error.
Protocol loadProtocolFile(const std::string& path, const ArenaConfig& arenaConfig);

// Writes a protocol to <folder>/<name>.json, creating the folder if needed.
// Throws std::runtime_error if the file cannot be written.
void saveProtocolFile(const std::string& folder, const Protocol& protocol);

// Deletes <folder>/<name>.json. Returns false if the file did not exist.
bool deleteProtocolFile(const std::string& folder, const std::string& name);

// Returns the names (without ".json") of all protocol files in a folder,
// sorted alphabetically. Returns an empty list if the folder does not exist.
std::vector<std::string> listProtocolNames(const std::string& folder);

}  // namespace buridan
