// =============================================================================
// StatusPublisher.cpp
//
// Implementation of the status / event publisher. See StatusPublisher.h.
// =============================================================================
#include "StatusPublisher.h"

#include <chrono>
#include <cstdio>
#include <string>
#include <thread>

namespace buridan {

namespace {

// How often queued events are checked and sent.
constexpr std::chrono::milliseconds kEventPollInterval(5);

}  // namespace

StatusPublisher::StatusPublisher(zmq::context_t* context, int port, double statusRateHz, ServerState* state)
    : context_(context), port_(port), statusRateHz_(statusRateHz), state_(state) {}

void StatusPublisher::run() {
  zmq::socket_t socket(*context_, zmq::socket_type::pub);
  socket.set(zmq::sockopt::linger, 0);
  const std::string address = "tcp://*:" + std::to_string(port_);
  socket.bind(address);
  std::fprintf(stderr, "[status] publishing on %s at %.1f Hz\n", address.c_str(), statusRateHz_);

  const std::chrono::duration<double> statusPeriodSeconds(1.0 / statusRateHz_);
  const std::chrono::steady_clock::duration statusPeriod =
      std::chrono::duration_cast<std::chrono::steady_clock::duration>(statusPeriodSeconds);
  std::chrono::steady_clock::time_point nextStatusTime = std::chrono::steady_clock::now();

  while (!state_->isStopRequested()) {
    // 1) Send every event that happened since the last loop, in order.
    const std::vector<nlohmann::json> events = state_->takeEvents();
    for (const nlohmann::json& event : events) {
      const std::string payload = event.dump();
      sendMessage(&socket, "event", payload);
    }

    // 2) Send a periodic status message when it is due.
    const std::chrono::steady_clock::time_point now = std::chrono::steady_clock::now();
    if (now >= nextStatusTime) {
      const StatusSnapshot snapshot = state_->status();
      nlohmann::json status = nlohmann::json::object();
      status["mode"] = displayModeToString(snapshot.mode);
      status["frame"] = snapshot.frameCount;
      status["fps"] = snapshot.measuredFrameRateHz;
      status["protocol"] = snapshot.protocolName;
      status["epoch"] = snapshot.epochIndex;
      status["epoch_type"] = snapshot.epochType;
      status["streamed_frames_received"] = snapshot.streamedFramesReceived;
      status["streamed_frames_rejected"] = snapshot.streamedFramesRejected;
      status["pwm_bits"] = snapshot.pwmBits;
      status["pwm_lsb_ns"] = snapshot.pwmLsbNanoseconds;
      status["hardware"] = snapshot.hardwareConnected;
      status["t_mono"] = monotonicSecondsSinceStart();
      status["t_wall"] = unixTimeSeconds();
      const std::string payload = status.dump();
      sendMessage(&socket, "status", payload);

      nextStatusTime += statusPeriod;
      // If we fell far behind (e.g. system was busy), do not try to catch up.
      if (nextStatusTime < now) {
        nextStatusTime = now + statusPeriod;
      }
    }

    std::this_thread::sleep_for(kEventPollInterval);
  }

  std::fprintf(stderr, "[status] stopped\n");
}

void StatusPublisher::sendMessage(zmq::socket_t* socket, const std::string& topic, const std::string& payload) {
  socket->send(zmq::buffer(topic), zmq::send_flags::sndmore);
  socket->send(zmq::buffer(payload), zmq::send_flags::none);
}

}  // namespace buridan
