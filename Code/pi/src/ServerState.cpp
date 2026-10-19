// =============================================================================
// ServerState.cpp
//
// Implementation of the thread-safe shared state. See ServerState.h.
// =============================================================================
#include "ServerState.h"

namespace buridan {

namespace {

// Maximum number of unpublished events kept in memory. If the status thread
// cannot keep up, the oldest events are dropped rather than using all memory.
constexpr std::size_t kMaxQueuedEvents = 1000;

// Monotonic time point captured when the program starts.
const std::chrono::steady_clock::time_point kServerStartTime = std::chrono::steady_clock::now();

}  // namespace

std::string displayModeToString(DisplayMode mode) {
  switch (mode) {
    case DisplayMode::Blank:
      return "blank";
    case DisplayMode::Protocol:
      return "protocol";
    case DisplayMode::Stream:
      return "stream";
    case DisplayMode::TestPattern:
      return "test_pattern";
  }
  return "unknown";
}

bool displayModeFromString(const std::string& text, DisplayMode* mode) {
  if (text == "blank") {
    *mode = DisplayMode::Blank;
    return true;
  }
  if (text == "protocol") {
    *mode = DisplayMode::Protocol;
    return true;
  }
  if (text == "stream") {
    *mode = DisplayMode::Stream;
    return true;
  }
  if (text == "test_pattern") {
    *mode = DisplayMode::TestPattern;
    return true;
  }
  return false;
}

double monotonicSecondsSinceStart() {
  const std::chrono::steady_clock::time_point now = std::chrono::steady_clock::now();
  const std::chrono::duration<double> elapsed = now - kServerStartTime;
  return elapsed.count();
}

double unixTimeSeconds() {
  const std::chrono::system_clock::time_point now = std::chrono::system_clock::now();
  const std::chrono::duration<double> sinceEpoch = now.time_since_epoch();
  return sinceEpoch.count();
}

void ServerState::pushRequest(const DisplayRequest& request) {
  std::lock_guard<std::mutex> lock(mutex_);
  requests_.push_back(request);
}

std::vector<DisplayRequest> ServerState::takeRequests() {
  std::lock_guard<std::mutex> lock(mutex_);
  std::vector<DisplayRequest> taken(requests_.begin(), requests_.end());
  requests_.clear();
  return taken;
}

void ServerState::requestPwmBits(int bits) {
  std::lock_guard<std::mutex> lock(mutex_);
  pendingPwmBits_ = bits;
}

int ServerState::takePwmBitsRequest() {
  std::lock_guard<std::mutex> lock(mutex_);
  const int bits = pendingPwmBits_;
  pendingPwmBits_ = 0;
  return bits;
}

bool ServerState::storeStreamedFrame(const uint8_t* data, std::size_t size) {
  std::lock_guard<std::mutex> lock(mutex_);
  const bool accepted = latestStreamedFrame_.copyFromPackedRgb(data, size);
  if (accepted) {
    hasStreamedFrame_ = true;
    status_.streamedFramesReceived += 1;
  } else {
    status_.streamedFramesRejected += 1;
  }
  return accepted;
}

bool ServerState::copyLatestStreamedFrame(RingFrame* frame) {
  std::lock_guard<std::mutex> lock(mutex_);
  if (!hasStreamedFrame_) {
    return false;
  }
  *frame = latestStreamedFrame_;
  return true;
}

void ServerState::updateStatus(const StatusSnapshot& snapshot) {
  std::lock_guard<std::mutex> lock(mutex_);
  // Keep the counters owned by the frame thread.
  const uint64_t received = status_.streamedFramesReceived;
  const uint64_t rejected = status_.streamedFramesRejected;
  status_ = snapshot;
  status_.streamedFramesReceived = received;
  status_.streamedFramesRejected = rejected;
}

StatusSnapshot ServerState::status() {
  std::lock_guard<std::mutex> lock(mutex_);
  return status_;
}

void ServerState::pushEvent(nlohmann::json event) {
  // Timestamps are added here so every event is stamped the same way.
  event["t_mono"] = monotonicSecondsSinceStart();
  event["t_wall"] = unixTimeSeconds();

  std::lock_guard<std::mutex> lock(mutex_);
  events_.push_back(event);
  while (events_.size() > kMaxQueuedEvents) {
    events_.pop_front();
  }
}

std::vector<nlohmann::json> ServerState::takeEvents() {
  std::lock_guard<std::mutex> lock(mutex_);
  std::vector<nlohmann::json> taken(events_.begin(), events_.end());
  events_.clear();
  return taken;
}

void ServerState::requestStop() {
  stopRequested_.store(true);
}

bool ServerState::isStopRequested() const {
  return stopRequested_.load();
}

}  // namespace buridan
