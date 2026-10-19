// =============================================================================
// Protocol.cpp
//
// Parsing, validation and file storage of protocols. See Protocol.h.
// =============================================================================
#include "Protocol.h"

#include <algorithm>
#include <cctype>
#include <filesystem>
#include <fstream>
#include <stdexcept>

#include "Stimulus.h"

namespace buridan {

namespace {

// Longest protocol name accepted.
constexpr std::size_t kMaxProtocolNameLength = 64;

// File extension used for protocol files.
const char* const kProtocolFileExtension = ".json";

}  // namespace

bool isValidProtocolName(const std::string& name) {
  if (name.empty()) {
    return false;
  }
  if (name.size() > kMaxProtocolNameLength) {
    return false;
  }
  for (const char character : name) {
    const unsigned char code = static_cast<unsigned char>(character);
    const bool isLetterOrDigit = (std::isalnum(code) != 0);
    const bool isAllowedSymbol = (character == '_') || (character == '-');
    if (!isLetterOrDigit && !isAllowedSymbol) {
      return false;
    }
  }
  return true;
}

Protocol parseProtocol(const nlohmann::json& document, const ArenaConfig& arenaConfig) {
  if (!document.is_object()) {
    throw std::invalid_argument("protocol must be a JSON object");
  }

  Protocol protocol;

  // --- name -----------------------------------------------------------------
  if (!document.contains("name") || !document.at("name").is_string()) {
    throw std::invalid_argument("protocol 'name' is missing or not a string");
  }
  protocol.name = document.at("name").get<std::string>();
  if (!isValidProtocolName(protocol.name)) {
    throw std::invalid_argument("protocol 'name' may only contain letters, digits, '_' and '-' (max 64)");
  }

  // --- schema_version -------------------------------------------------------
  if (document.contains("schema_version")) {
    if (!document.at("schema_version").is_number_integer()) {
      throw std::invalid_argument("'schema_version' must be an integer");
    }
    protocol.schemaVersion = document.at("schema_version").get<int>();
  }
  if (protocol.schemaVersion != kProtocolSchemaVersion) {
    throw std::invalid_argument("unsupported 'schema_version' (expected 1)");
  }

  // --- frame_rate_hz --------------------------------------------------------
  protocol.frameRateHz = readNumber(document, "frame_rate_hz", kDefaultProtocolFrameRateHz);
  if (protocol.frameRateHz < kMinFrameRateHz || protocol.frameRateHz > kMaxFrameRateHz) {
    throw std::invalid_argument("'frame_rate_hz' must be between 1 and 240");
  }

  // --- epochs ---------------------------------------------------------------
  if (!document.contains("epochs") || !document.at("epochs").is_array()) {
    throw std::invalid_argument("protocol 'epochs' is missing or not an array");
  }
  const nlohmann::json& epochList = document.at("epochs");
  if (epochList.empty()) {
    throw std::invalid_argument("protocol must contain at least one epoch");
  }

  for (std::size_t epochIndex = 0; epochIndex < epochList.size(); ++epochIndex) {
    const nlohmann::json& epochJson = epochList.at(epochIndex);
    const std::string where = "epoch " + std::to_string(epochIndex) + ": ";

    if (!epochJson.is_object()) {
      throw std::invalid_argument(where + "must be a JSON object");
    }

    Epoch epoch;

    if (!epochJson.contains("type") || !epochJson.at("type").is_string()) {
      throw std::invalid_argument(where + "'type' is missing or not a string");
    }
    epoch.type = epochJson.at("type").get<std::string>();

    if (!epochJson.contains("duration_s") || !epochJson.at("duration_s").is_number()) {
      throw std::invalid_argument(where + "'duration_s' is missing or not a number");
    }
    epoch.durationSeconds = epochJson.at("duration_s").get<double>();
    if (epoch.durationSeconds <= 0.0) {
      throw std::invalid_argument(where + "'duration_s' must be greater than 0");
    }

    if (epochJson.contains("params")) {
      if (!epochJson.at("params").is_object()) {
        throw std::invalid_argument(where + "'params' must be a JSON object");
      }
      epoch.params = epochJson.at("params");
    }

    // Create the stimulus once to validate the parameters now, rather than
    // failing halfway through an experiment.
    try {
      std::unique_ptr<Stimulus> probe = createStimulus(epoch.type, epoch.params, arenaConfig);
    } catch (const std::invalid_argument& error) {
      throw std::invalid_argument(where + error.what());
    }

    protocol.epochs.push_back(epoch);
  }

  return protocol;
}

nlohmann::json protocolToJson(const Protocol& protocol) {
  nlohmann::json document = nlohmann::json::object();
  document["name"] = protocol.name;
  document["schema_version"] = protocol.schemaVersion;
  document["frame_rate_hz"] = protocol.frameRateHz;

  nlohmann::json epochList = nlohmann::json::array();
  for (const Epoch& epoch : protocol.epochs) {
    nlohmann::json epochJson = nlohmann::json::object();
    epochJson["type"] = epoch.type;
    epochJson["duration_s"] = epoch.durationSeconds;
    epochJson["params"] = epoch.params;
    epochList.push_back(epochJson);
  }
  document["epochs"] = epochList;

  return document;
}

std::string protocolFilePath(const std::string& folder, const std::string& name) {
  std::filesystem::path path(folder);
  path /= name + kProtocolFileExtension;
  return path.string();
}

Protocol loadProtocolFile(const std::string& path, const ArenaConfig& arenaConfig) {
  std::ifstream file(path);
  if (!file.is_open()) {
    throw std::invalid_argument("cannot open protocol file: " + path);
  }

  nlohmann::json document;
  try {
    document = nlohmann::json::parse(file);
  } catch (const nlohmann::json::exception& error) {
    throw std::invalid_argument(std::string("invalid JSON in protocol file: ") + error.what());
  }

  return parseProtocol(document, arenaConfig);
}

void saveProtocolFile(const std::string& folder, const Protocol& protocol) {
  // Create the folder (and its parents) if it does not exist yet.
  std::filesystem::create_directories(folder);

  const std::string finalPath = protocolFilePath(folder, protocol.name);
  const std::string temporaryPath = finalPath + ".tmp";

  // Write to a temporary file first, then rename it over the final file, so a
  // power cut never leaves a half-written protocol behind.
  {
    std::ofstream file(temporaryPath, std::ios::trunc);
    if (!file.is_open()) {
      throw std::runtime_error("cannot write protocol file: " + temporaryPath);
    }
    const nlohmann::json document = protocolToJson(protocol);
    const int indentSpaces = 2;
    file << document.dump(indentSpaces) << "\n";
    if (!file.good()) {
      throw std::runtime_error("error while writing protocol file: " + temporaryPath);
    }
  }
  std::filesystem::rename(temporaryPath, finalPath);
}

bool deleteProtocolFile(const std::string& folder, const std::string& name) {
  const std::string path = protocolFilePath(folder, name);
  return std::filesystem::remove(path);
}

std::vector<std::string> listProtocolNames(const std::string& folder) {
  std::vector<std::string> names;

  std::error_code errorCode;
  const bool folderExists = std::filesystem::is_directory(folder, errorCode);
  if (!folderExists) {
    return names;
  }

  for (const std::filesystem::directory_entry& entry : std::filesystem::directory_iterator(folder)) {
    if (!entry.is_regular_file()) {
      continue;
    }
    const std::filesystem::path& path = entry.path();
    if (path.extension() != kProtocolFileExtension) {
      continue;
    }
    const std::string name = path.stem().string();
    if (!isValidProtocolName(name)) {
      continue;
    }
    names.push_back(name);
  }

  std::sort(names.begin(), names.end());
  return names;
}

}  // namespace buridan
