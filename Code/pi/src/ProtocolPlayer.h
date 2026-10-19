// =============================================================================
// ProtocolPlayer.h
//
// Plays a Protocol: keeps track of which epoch is active and asks the current
// stimulus to render itself.
//
// Epoch boundaries are computed from the protocol start time plus the summed
// epoch durations, so timing errors never accumulate from frame to frame.
//
// The player is used only by the render thread and is not thread-safe.
// =============================================================================
#pragma once

#include <chrono>
#include <memory>
#include <string>
#include <vector>

#include "ArenaConfig.h"
#include "Protocol.h"
#include "RingFrame.h"
#include "Stimulus.h"

namespace buridan {

using SteadyClock = std::chrono::steady_clock;

// Something that happened while playing, to be reported to the PC.
struct PlayerEvent {
  // "protocol_start", "epoch_start" or "protocol_end".
  std::string kind;
  std::string protocolName;
  int epochIndex = -1;
  std::string epochType;
};

class ProtocolPlayer {
 public:
  explicit ProtocolPlayer(const ArenaConfig& arenaConfig);

  // Starts playing a protocol from its first epoch at time 'now'.
  // Adds "protocol_start" and "epoch_start" events to 'events'.
  void start(const Protocol& protocol, SteadyClock::time_point now, std::vector<PlayerEvent>* events);

  // Stops playing immediately (no event is generated).
  void stop();

  // Renders the frame for time 'now'. Advances through epochs as needed and
  // adds "epoch_start" / "protocol_end" events to 'events'.
  // When the protocol has finished, the frame is filled with black.
  void render(RingFrame* frame, SteadyClock::time_point now, std::vector<PlayerEvent>* events);

  // True while a protocol is playing.
  bool isRunning() const;

  // Name of the protocol being played (empty if none).
  std::string protocolName() const;

  // Index of the current epoch (-1 if none).
  int epochIndex() const;

  // Type of the current epoch (empty if none).
  std::string epochType() const;

  // Frame rate requested by the current protocol.
  double frameRateHz() const;

 private:
  // Makes epoch 'index' current and creates its stimulus.
  void enterEpoch(std::size_t index, std::vector<PlayerEvent>* events);

  ArenaConfig arenaConfig_;
  Protocol protocol_;
  bool running_ = false;
  std::size_t currentEpoch_ = 0;
  SteadyClock::time_point currentEpochStart_;
  SteadyClock::time_point currentEpochEnd_;
  std::unique_ptr<Stimulus> currentStimulus_;
};

}  // namespace buridan
