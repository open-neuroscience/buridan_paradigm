// =============================================================================
// ProtocolPlayer.cpp
//
// Implementation of the protocol player. See ProtocolPlayer.h.
// =============================================================================
#include "ProtocolPlayer.h"

namespace buridan {

namespace {

// Converts a duration in seconds (double) into a steady-clock duration.
SteadyClock::duration secondsToDuration(double seconds) {
  const std::chrono::duration<double> floatingSeconds(seconds);
  return std::chrono::duration_cast<SteadyClock::duration>(floatingSeconds);
}

}  // namespace

ProtocolPlayer::ProtocolPlayer(const ArenaConfig& arenaConfig) : arenaConfig_(arenaConfig) {}

void ProtocolPlayer::start(const Protocol& protocol, SteadyClock::time_point now, std::vector<PlayerEvent>* events) {
  protocol_ = protocol;
  running_ = true;

  PlayerEvent startEvent;
  startEvent.kind = "protocol_start";
  startEvent.protocolName = protocol_.name;
  events->push_back(startEvent);

  // The first epoch starts now; later epochs start when the previous one ends.
  currentEpochEnd_ = now;
  enterEpoch(0, events);
}

void ProtocolPlayer::stop() {
  running_ = false;
  currentStimulus_.reset();
}

void ProtocolPlayer::render(RingFrame* frame, SteadyClock::time_point now, std::vector<PlayerEvent>* events) {
  if (!running_) {
    const Rgb black;
    frame->fill(black);
    return;
  }

  // Move on to the next epoch(s) if the current one is over. A loop is used
  // in case the render thread was delayed past more than one short epoch.
  while (running_ && now >= currentEpochEnd_) {
    const std::size_t nextEpoch = currentEpoch_ + 1;
    if (nextEpoch >= protocol_.epochs.size()) {
      PlayerEvent endEvent;
      endEvent.kind = "protocol_end";
      endEvent.protocolName = protocol_.name;
      events->push_back(endEvent);
      stop();
      break;
    }
    enterEpoch(nextEpoch, events);
  }

  if (!running_) {
    const Rgb black;
    frame->fill(black);
    return;
  }

  const std::chrono::duration<double> elapsed = now - currentEpochStart_;
  const double secondsSinceEpochStart = elapsed.count();
  currentStimulus_->render(frame, secondsSinceEpochStart);
}

bool ProtocolPlayer::isRunning() const {
  return running_;
}

std::string ProtocolPlayer::protocolName() const {
  if (!running_) {
    return std::string();
  }
  return protocol_.name;
}

int ProtocolPlayer::epochIndex() const {
  if (!running_) {
    return -1;
  }
  return static_cast<int>(currentEpoch_);
}

std::string ProtocolPlayer::epochType() const {
  if (!running_) {
    return std::string();
  }
  return protocol_.epochs[currentEpoch_].type;
}

double ProtocolPlayer::frameRateHz() const {
  return protocol_.frameRateHz;
}

void ProtocolPlayer::enterEpoch(std::size_t index, std::vector<PlayerEvent>* events) {
  currentEpoch_ = index;
  const Epoch& epoch = protocol_.epochs[index];

  // The new epoch starts exactly where the previous one was scheduled to end.
  currentEpochStart_ = currentEpochEnd_;
  const SteadyClock::duration epochDuration = secondsToDuration(epoch.durationSeconds);
  currentEpochEnd_ = currentEpochStart_ + epochDuration;

  // Parameters were validated when the protocol was parsed, so this succeeds.
  currentStimulus_ = createStimulus(epoch.type, epoch.params, arenaConfig_);

  PlayerEvent epochEvent;
  epochEvent.kind = "epoch_start";
  epochEvent.protocolName = protocol_.name;
  epochEvent.epochIndex = static_cast<int>(index);
  epochEvent.epochType = epoch.type;
  events->push_back(epochEvent);
}

}  // namespace buridan
