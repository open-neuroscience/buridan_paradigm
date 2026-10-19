// =============================================================================
// CommandServer.cpp
//
// Implementation of the JSON command interface. See CommandServer.h.
// =============================================================================
#include "CommandServer.h"

#include <cstdio>
#include <stdexcept>
#include <string>

#include "Protocol.h"

namespace buridan {

const char* const kServerVersion = "0.1.0";

namespace {

// How long recv() waits before checking whether the server should stop.
constexpr int kReceiveTimeoutMilliseconds = 200;

// Builds the "tcp://*:<port>" address used to bind a socket on all interfaces.
std::string bindAddress(int port) {
  return "tcp://*:" + std::to_string(port);
}

}  // namespace

CommandServer::CommandServer(zmq::context_t* context,
                             int port,
                             const std::string& protocolFolder,
                             const ArenaConfig& arenaConfig,
                             ServerState* state)
    : context_(context),
      port_(port),
      protocolFolder_(protocolFolder),
      arenaConfig_(arenaConfig),
      state_(state) {}

void CommandServer::run() {
  zmq::socket_t socket(*context_, zmq::socket_type::rep);
  socket.set(zmq::sockopt::linger, 0);
  socket.set(zmq::sockopt::rcvtimeo, kReceiveTimeoutMilliseconds);
  const std::string address = bindAddress(port_);
  socket.bind(address);
  std::fprintf(stderr, "[command] listening on %s\n", address.c_str());

  while (!state_->isStopRequested()) {
    // Wait for the next request (or a timeout, to re-check the stop flag).
    zmq::message_t requestMessage;
    const zmq::recv_result_t received = socket.recv(requestMessage, zmq::recv_flags::none);
    if (!received.has_value()) {
      continue;
    }

    // A REP socket must answer every request exactly once, so any extra
    // message parts are read and ignored.
    while (requestMessage.more()) {
      zmq::message_t extraPart;
      const zmq::recv_result_t extraReceived = socket.recv(extraPart, zmq::recv_flags::none);
      if (!extraReceived.has_value()) {
        break;
      }
      if (!extraPart.more()) {
        break;
      }
    }

    const std::string requestText = requestMessage.to_string();
    const nlohmann::json reply = handleRequest(requestText);
    const std::string replyText = reply.dump();
    socket.send(zmq::buffer(replyText), zmq::send_flags::none);
  }

  std::fprintf(stderr, "[command] stopped\n");
}

nlohmann::json CommandServer::handleRequest(const std::string& requestText) {
  std::string command = "unknown";
  try {
    const nlohmann::json request = nlohmann::json::parse(requestText);
    if (!request.is_object() || !request.contains("cmd") || !request.at("cmd").is_string()) {
      throw std::invalid_argument("request must be a JSON object with a string 'cmd'");
    }
    command = request.at("cmd").get<std::string>();

    nlohmann::json reply;
    if (command == "ping") {
      reply = handlePing();
    } else if (command == "status") {
      reply = handleStatus();
    } else if (command == "list_protocols") {
      reply = handleListProtocols();
    } else if (command == "get_protocol") {
      reply = handleGetProtocol(request);
    } else if (command == "upload_protocol") {
      reply = handleUploadProtocol(request);
    } else if (command == "delete_protocol") {
      reply = handleDeleteProtocol(request);
    } else if (command == "start_protocol") {
      reply = handleStartProtocol(request);
    } else if (command == "stop") {
      reply = handleStop();
    } else if (command == "set_mode") {
      reply = handleSetMode(request);
    } else if (command == "set_pwm_bits") {
      reply = handleSetPwmBits(request);
    } else if (command == "shutdown") {
      reply = handleShutdown();
    } else {
      throw std::invalid_argument("unknown command '" + command + "'");
    }

    reply["ok"] = true;
    reply["cmd"] = command;
    return reply;
  } catch (const std::exception& error) {
    // Any failure becomes an error reply; the server keeps running.
    nlohmann::json reply = nlohmann::json::object();
    reply["ok"] = false;
    reply["cmd"] = command;
    reply["error"] = error.what();
    std::fprintf(stderr, "[command] '%s' failed: %s\n", command.c_str(), error.what());
    return reply;
  }
}

nlohmann::json CommandServer::handlePing() {
  nlohmann::json reply = nlohmann::json::object();
  reply["reply"] = "pong";
  reply["version"] = kServerVersion;
  reply["t_mono"] = monotonicSecondsSinceStart();
  reply["t_wall"] = unixTimeSeconds();
  return reply;
}

nlohmann::json CommandServer::handleStatus() {
  const StatusSnapshot snapshot = state_->status();
  nlohmann::json reply = nlohmann::json::object();
  reply["mode"] = displayModeToString(snapshot.mode);
  reply["frame"] = snapshot.frameCount;
  reply["fps"] = snapshot.measuredFrameRateHz;
  reply["protocol"] = snapshot.protocolName;
  reply["epoch"] = snapshot.epochIndex;
  reply["epoch_type"] = snapshot.epochType;
  reply["streamed_frames_received"] = snapshot.streamedFramesReceived;
  reply["streamed_frames_rejected"] = snapshot.streamedFramesRejected;
  reply["pwm_bits"] = snapshot.pwmBits;
  reply["pwm_lsb_ns"] = snapshot.pwmLsbNanoseconds;
  reply["hardware"] = snapshot.hardwareConnected;
  reply["t_mono"] = monotonicSecondsSinceStart();
  reply["t_wall"] = unixTimeSeconds();
  return reply;
}

nlohmann::json CommandServer::handleListProtocols() {
  const std::vector<std::string> names = listProtocolNames(protocolFolder_);
  nlohmann::json reply = nlohmann::json::object();
  reply["protocols"] = names;
  return reply;
}

nlohmann::json CommandServer::handleGetProtocol(const nlohmann::json& request) {
  const std::string name = readProtocolName(request);
  const std::string path = protocolFilePath(protocolFolder_, name);
  const Protocol protocol = loadProtocolFile(path, arenaConfig_);
  nlohmann::json reply = nlohmann::json::object();
  reply["protocol"] = protocolToJson(protocol);
  return reply;
}

nlohmann::json CommandServer::handleUploadProtocol(const nlohmann::json& request) {
  if (!request.contains("protocol")) {
    throw std::invalid_argument("'protocol' is missing");
  }
  // parseProtocol validates everything before anything is written to disk.
  const Protocol protocol = parseProtocol(request.at("protocol"), arenaConfig_);
  saveProtocolFile(protocolFolder_, protocol);

  nlohmann::json event = nlohmann::json::object();
  event["event"] = "protocol_saved";
  event["protocol"] = protocol.name;
  state_->pushEvent(event);

  nlohmann::json reply = nlohmann::json::object();
  reply["saved"] = protocol.name;
  return reply;
}

nlohmann::json CommandServer::handleDeleteProtocol(const nlohmann::json& request) {
  const std::string name = readProtocolName(request);
  const bool deleted = deleteProtocolFile(protocolFolder_, name);
  if (!deleted) {
    throw std::invalid_argument("protocol '" + name + "' does not exist");
  }
  nlohmann::json reply = nlohmann::json::object();
  reply["deleted"] = name;
  return reply;
}

nlohmann::json CommandServer::handleStartProtocol(const nlohmann::json& request) {
  Protocol protocol;
  if (request.contains("protocol")) {
    // Inline protocol: played without being saved.
    protocol = parseProtocol(request.at("protocol"), arenaConfig_);
  } else {
    // Named protocol: loaded from the protocol folder.
    const std::string name = readProtocolName(request);
    const std::string path = protocolFilePath(protocolFolder_, name);
    protocol = loadProtocolFile(path, arenaConfig_);
  }

  DisplayRequest displayRequest;
  displayRequest.mode = DisplayMode::Protocol;
  displayRequest.protocol = protocol;
  state_->pushRequest(displayRequest);

  // Reply with the full protocol so the PC can store exactly what was played.
  nlohmann::json reply = nlohmann::json::object();
  reply["started"] = protocol.name;
  reply["protocol"] = protocolToJson(protocol);
  return reply;
}

nlohmann::json CommandServer::handleStop() {
  DisplayRequest displayRequest;
  displayRequest.mode = DisplayMode::Blank;
  state_->pushRequest(displayRequest);
  return nlohmann::json::object();
}

nlohmann::json CommandServer::handleSetMode(const nlohmann::json& request) {
  if (!request.contains("mode") || !request.at("mode").is_string()) {
    throw std::invalid_argument("'mode' is missing or not a string");
  }
  const std::string modeText = request.at("mode").get<std::string>();

  DisplayMode mode = DisplayMode::Blank;
  const bool known = displayModeFromString(modeText, &mode);
  if (!known) {
    throw std::invalid_argument("unknown mode '" + modeText + "'");
  }
  if (mode == DisplayMode::Protocol) {
    throw std::invalid_argument("use 'start_protocol' to play a protocol");
  }

  DisplayRequest displayRequest;
  displayRequest.mode = mode;
  state_->pushRequest(displayRequest);

  nlohmann::json reply = nlohmann::json::object();
  reply["mode"] = modeText;
  return reply;
}

nlohmann::json CommandServer::handleSetPwmBits(const nlohmann::json& request) {
  if (!request.contains("bits") || !request.at("bits").is_number_integer()) {
    throw std::invalid_argument("'bits' is missing or not an integer");
  }
  const int bits = request.at("bits").get<int>();
  const bool inRange = (bits >= kMinPwmBits) && (bits <= kMaxPwmBits);
  if (!inRange) {
    throw std::invalid_argument("'bits' must be between " + std::to_string(kMinPwmBits) + " and " +
                                std::to_string(kMaxPwmBits));
  }

  // The render thread owns the matrix and applies the change before its next
  // frame. The "pwm_bits" status field shows when it has been applied.
  state_->requestPwmBits(bits);

  nlohmann::json reply = nlohmann::json::object();
  reply["pwm_bits"] = bits;
  return reply;
}

nlohmann::json CommandServer::handleShutdown() {
  state_->requestStop();
  return nlohmann::json::object();
}

std::string CommandServer::readProtocolName(const nlohmann::json& request) {
  if (!request.contains("name") || !request.at("name").is_string()) {
    throw std::invalid_argument("'name' is missing or not a string");
  }
  const std::string name = request.at("name").get<std::string>();
  if (!isValidProtocolName(name)) {
    throw std::invalid_argument("invalid protocol name '" + name + "'");
  }
  return name;
}

}  // namespace buridan
