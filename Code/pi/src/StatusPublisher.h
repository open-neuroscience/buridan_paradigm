// =============================================================================
// StatusPublisher.h
//
// Publishes status updates and events to the PC over a ZeroMQ PUB socket.
//
// Every message has two parts:
//   part 1: topic, either "status" or "event"
//   part 2: JSON payload
//
// "status" messages are sent at a fixed rate (default 10 Hz) and contain the
// display mode, frame counter, measured frame rate, protocol and epoch.
// "event" messages are sent as soon as something happens, e.g.
//   {"event": "epoch_start", "protocol": "...", "epoch": 1, "epoch_type": "grating", ...}
// Both carry "t_mono" (seconds since server start) and "t_wall" (Unix time).
// Bonsai logs these together with camera timestamps for synchronisation.
// =============================================================================
#pragma once

#include <zmq.hpp>

#include "ServerState.h"

namespace buridan {

class StatusPublisher {
 public:
  StatusPublisher(zmq::context_t* context, int port, double statusRateHz, ServerState* state);

  // Thread body: publishes until state->isStopRequested() is true.
  void run();

 private:
  // Sends one two-part message (topic, JSON payload).
  static void sendMessage(zmq::socket_t* socket, const std::string& topic, const std::string& payload);

  zmq::context_t* context_;
  int port_;
  double statusRateHz_;
  ServerState* state_;
};

}  // namespace buridan
