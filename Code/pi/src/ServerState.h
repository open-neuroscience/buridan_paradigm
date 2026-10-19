// =============================================================================
// ServerState.h
//
// Data shared between the threads of buridan_server:
//
//   render thread  (main)      : draws frames, owns the ProtocolPlayer
//   command thread             : receives commands from the PC
//   frame thread               : receives streamed frames from the PC
//   status thread              : publishes status and events to the PC
//
// Every member is protected by a single mutex. Threads never hold the lock
// while doing slow work (rendering, network I/O); they copy what they need.
// =============================================================================
#pragma once

#include <atomic>
#include <chrono>
#include <cstdint>
#include <deque>
#include <mutex>
#include <string>
#include <vector>

#include <nlohmann/json.hpp>

#include "Protocol.h"
#include "RingFrame.h"

namespace buridan {

// Range of PWM bits (colour bit depth per channel) accepted by the
// rpi-rgb-led-matrix library. Fewer bits give fewer grey levels
// (2^bits per channel) but a faster panel refresh.
constexpr int kMinPwmBits = 1;
constexpr int kMaxPwmBits = 11;

// What the panels are currently showing.
enum class DisplayMode {
  Blank,        // all LEDs off
  Protocol,     // a protocol is playing on the Pi
  Stream,       // frames streamed from the PC are shown
  TestPattern,  // panel identification pattern
};

// Converts a DisplayMode to the string used in JSON messages.
std::string displayModeToString(DisplayMode mode);

// Converts a JSON mode string into a DisplayMode.
// Returns false if the string is not a known mode.
bool displayModeFromString(const std::string& text, DisplayMode* mode);

// A request from the command thread to the render thread.
struct DisplayRequest {
  // Mode to switch to.
  DisplayMode mode = DisplayMode::Blank;
  // Protocol to play, only used when mode is DisplayMode::Protocol.
  Protocol protocol;
};

// A snapshot of what the render thread is doing, for status replies.
struct StatusSnapshot {
  DisplayMode mode = DisplayMode::Blank;
  uint64_t frameCount = 0;
  double measuredFrameRateHz = 0.0;
  std::string protocolName;
  int epochIndex = -1;
  std::string epochType;
  uint64_t streamedFramesReceived = 0;
  uint64_t streamedFramesRejected = 0;
  // PWM bits currently used by the panels.
  int pwmBits = kMaxPwmBits;
  // Duration of the shortest PWM step in nanoseconds (--led-pwm-lsb-nanoseconds).
  // Fixed when the server starts; shorter steps give a faster refresh.
  int pwmLsbNanoseconds = 0;
  // False when running with --no-hardware (PWM bits then have no effect).
  bool hardwareConnected = true;
};

// Time helpers shared by all threads ------------------------------------------

// Seconds elapsed on the monotonic clock since the server started.
double monotonicSecondsSinceStart();

// Current wall-clock time as seconds since 1970-01-01 UTC.
double unixTimeSeconds();

class ServerState {
 public:
  // --- requests: command thread -> render thread ------------------------------

  // Queues a display request. Requests are applied in order.
  void pushRequest(const DisplayRequest& request);

  // Removes and returns all queued requests (render thread).
  std::vector<DisplayRequest> takeRequests();

  // --- PWM bits: command thread -> render thread ------------------------------

  // Asks the render thread to switch the panels to 'bits' PWM bits.
  // Only the newest request is kept.
  void requestPwmBits(int bits);

  // Removes and returns the pending PWM bits request (render thread).
  // Returns 0 when there is no pending request.
  int takePwmBitsRequest();

  // --- streamed frames: frame thread -> render thread -------------------------

  // Stores a newly received frame. Returns false if it has the wrong size.
  bool storeStreamedFrame(const uint8_t* data, std::size_t size);

  // Copies the latest streamed frame into 'frame'.
  // Returns false (and leaves 'frame' untouched) if no frame was received yet.
  bool copyLatestStreamedFrame(RingFrame* frame);

  // --- status: render thread -> command / status threads ----------------------

  // Replaces the current status (render thread). The streamed frame counters
  // are maintained by ServerState itself and are not overwritten.
  void updateStatus(const StatusSnapshot& snapshot);

  // Returns a copy of the current status.
  StatusSnapshot status();

  // --- events: any thread -> status thread ------------------------------------

  // Queues an event to be published to the PC. "t_mono" and "t_wall" are added.
  void pushEvent(nlohmann::json event);

  // Removes and returns all queued events (status thread).
  std::vector<nlohmann::json> takeEvents();

  // --- shutdown ---------------------------------------------------------------

  // Asks every thread to finish.
  void requestStop();

  // True once requestStop() has been called.
  bool isStopRequested() const;

 private:
  std::mutex mutex_;
  std::deque<DisplayRequest> requests_;
  // 0 means "no pending request".
  int pendingPwmBits_ = 0;
  RingFrame latestStreamedFrame_;
  bool hasStreamedFrame_ = false;
  StatusSnapshot status_;
  std::deque<nlohmann::json> events_;
  std::atomic<bool> stopRequested_{false};
};

}  // namespace buridan
