// =============================================================================
// CommandServer.h
//
// Receives commands from the PC over a ZeroMQ REP socket and replies to each.
//
// Every request is a single JSON string with a "cmd" field. Every reply is a
// single JSON string with "ok" (true/false), "cmd" and, on failure, "error".
//
// Commands (see docs/protocol-schema.md for full details):
//   ping                                  -> "reply": "pong", clocks, version
//   status                                -> current mode, frame counter, ...
//   list_protocols                        -> "protocols": [names]
//   get_protocol    {"name"}              -> "protocol": {...}
//   upload_protocol {"protocol": {...}}   -> saves <name>.json on the Pi
//   delete_protocol {"name"}              -> removes <name>.json
//   start_protocol  {"name"} or {"protocol": {...}}
//   stop                                  -> blank display
//   set_mode        {"mode": "blank" | "stream" | "test_pattern"}
//   shutdown                              -> stops buridan_server
// =============================================================================
#pragma once

#include <string>

#include <nlohmann/json.hpp>
#include <zmq.hpp>

#include "ArenaConfig.h"
#include "ServerState.h"

namespace buridan {

// Version string reported in "ping" replies.
extern const char* const kServerVersion;

class CommandServer {
 public:
  CommandServer(zmq::context_t* context,
                int port,
                const std::string& protocolFolder,
                const ArenaConfig& arenaConfig,
                ServerState* state);

  // Thread body: serves commands until state->isStopRequested() is true.
  void run();

 private:
  // Decodes one request string and returns the reply object.
  nlohmann::json handleRequest(const std::string& requestText);

  // Individual command handlers. Each returns the reply object and throws
  // std::invalid_argument / std::runtime_error to report an error.
  nlohmann::json handlePing();
  nlohmann::json handleStatus();
  nlohmann::json handleListProtocols();
  nlohmann::json handleGetProtocol(const nlohmann::json& request);
  nlohmann::json handleUploadProtocol(const nlohmann::json& request);
  nlohmann::json handleDeleteProtocol(const nlohmann::json& request);
  nlohmann::json handleStartProtocol(const nlohmann::json& request);
  nlohmann::json handleStop();
  nlohmann::json handleSetMode(const nlohmann::json& request);
  nlohmann::json handleSetPwmBits(const nlohmann::json& request);
  nlohmann::json handleShutdown();

  // Reads and validates request["name"]. Throws std::invalid_argument.
  std::string readProtocolName(const nlohmann::json& request);

  zmq::context_t* context_;
  int port_;
  std::string protocolFolder_;
  ArenaConfig arenaConfig_;
  ServerState* state_;
};

}  // namespace buridan
