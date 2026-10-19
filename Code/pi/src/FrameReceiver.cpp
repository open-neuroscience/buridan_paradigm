// =============================================================================
// FrameReceiver.cpp
//
// Implementation of the streamed-frame receiver. See FrameReceiver.h.
// =============================================================================
#include "FrameReceiver.h"

#include <cstdio>
#include <string>

namespace buridan {

namespace {

// How long recv() waits before checking whether the server should stop.
constexpr int kReceiveTimeoutMilliseconds = 200;

// Number of frames kept in the ZeroMQ receive queue. A small value keeps
// latency low: we only ever want the newest frame.
constexpr int kReceiveHighWaterMark = 2;

}  // namespace

FrameReceiver::FrameReceiver(zmq::context_t* context, int port, ServerState* state)
    : context_(context), port_(port), state_(state) {}

void FrameReceiver::run() {
  zmq::socket_t socket(*context_, zmq::socket_type::pull);
  socket.set(zmq::sockopt::linger, 0);
  socket.set(zmq::sockopt::rcvtimeo, kReceiveTimeoutMilliseconds);
  socket.set(zmq::sockopt::rcvhwm, kReceiveHighWaterMark);
  const std::string address = "tcp://*:" + std::to_string(port_);
  socket.bind(address);
  std::fprintf(stderr, "[frames] listening on %s\n", address.c_str());

  while (!state_->isStopRequested()) {
    zmq::message_t message;
    const zmq::recv_result_t received = socket.recv(message, zmq::recv_flags::none);
    if (!received.has_value()) {
      continue;
    }

    // Multi-part messages are not part of the frame format: drain and reject.
    if (message.more()) {
      bool morePartsPending = true;
      while (morePartsPending) {
        zmq::message_t extraPart;
        const zmq::recv_result_t extraReceived = socket.recv(extraPart, zmq::recv_flags::none);
        if (!extraReceived.has_value()) {
          break;
        }
        morePartsPending = extraPart.more();
      }
      state_->storeStreamedFrame(nullptr, 0);
      continue;
    }

    const uint8_t* data = static_cast<const uint8_t*>(message.data());
    const std::size_t size = message.size();
    const bool accepted = state_->storeStreamedFrame(data, size);
    if (!accepted) {
      std::fprintf(stderr, "[frames] rejected frame of %zu bytes (expected %d)\n", size, kRingFrameBytes);
    }
  }

  std::fprintf(stderr, "[frames] stopped\n");
}

}  // namespace buridan
