// =============================================================================
// FrameReceiver.h
//
// Receives complete ring frames streamed from the PC (e.g. rendered by
// BonVision) over a ZeroMQ PULL socket.
//
// Each message must be a single part of exactly kRingFrameBytes (73728) bytes
// in packed RGB layout (see RingFrame.h). Messages of any other size are
// counted as rejected and ignored.
//
// Only the most recent frame is kept: if the PC sends faster than the panels
// refresh, older frames are simply overwritten (lowest latency).
// Frames are only displayed while the server is in "stream" mode.
// =============================================================================
#pragma once

#include <zmq.hpp>

#include "ServerState.h"

namespace buridan {

class FrameReceiver {
 public:
  FrameReceiver(zmq::context_t* context, int port, ServerState* state);

  // Thread body: receives frames until state->isStopRequested() is true.
  void run();

 private:
  zmq::context_t* context_;
  int port_;
  ServerState* state_;
};

}  // namespace buridan
