// =============================================================================
// render_preview.cpp
//
// Renders frames of a protocol (or the test pattern) to image files, without
// any LED hardware. Useful to check a new protocol on any computer before
// running it in the arena.
//
// Usage:
//   render_preview <protocol.json> <output_prefix> [time_s ...]
//   render_preview --test-pattern <output.ppm>
//
// For each requested time (seconds since protocol start, default: 0) one
// binary PPM image (384 x 64, viewable with most image viewers) is written to
// <output_prefix>_<time>s.ppm.
// =============================================================================
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <string>
#include <vector>

#include "ArenaConfig.h"
#include "Protocol.h"
#include "ProtocolPlayer.h"
#include "RingFrame.h"
#include "TestPattern.h"

namespace {

// Writes a RingFrame as a binary PPM (P6) image. Returns false on failure.
bool writePpm(const buridan::RingFrame& frame, const std::string& path) {
  std::ofstream file(path, std::ios::binary);
  if (!file.is_open()) {
    return false;
  }
  file << "P6\n" << buridan::kRingWidth << " " << buridan::kRingHeight << "\n255\n";
  const std::vector<uint8_t>& bytes = frame.bytes();
  file.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
  return file.good();
}

}  // namespace

int main(int argc, char** argv) {
  if (argc < 3) {
    std::fprintf(stderr, "usage: %s <protocol.json> <output_prefix> [time_s ...]\n", argv[0]);
    std::fprintf(stderr, "       %s --test-pattern <output.ppm>\n", argv[0]);
    return 1;
  }

  const std::string firstArgument = argv[1];
  const std::string secondArgument = argv[2];

  // --- test pattern -----------------------------------------------------------
  if (firstArgument == "--test-pattern") {
    buridan::RingFrame frame;
    buridan::drawTestPattern(&frame);
    if (!writePpm(frame, secondArgument)) {
      std::fprintf(stderr, "error: cannot write %s\n", secondArgument.c_str());
      return 1;
    }
    std::printf("wrote %s\n", secondArgument.c_str());
    return 0;
  }

  // --- protocol frames ----------------------------------------------------------
  const buridan::ArenaConfig arenaConfig;
  buridan::Protocol protocol;
  try {
    protocol = buridan::loadProtocolFile(firstArgument, arenaConfig);
  } catch (const std::exception& error) {
    std::fprintf(stderr, "error: %s\n", error.what());
    return 1;
  }

  std::vector<double> times;
  for (int index = 3; index < argc; ++index) {
    const double time = std::atof(argv[index]);
    times.push_back(time);
  }
  if (times.empty()) {
    times.push_back(0.0);
  }

  for (const double timeSeconds : times) {
    // A fresh player for each time point, started at a fixed reference time.
    buridan::ProtocolPlayer player(arenaConfig);
    std::vector<buridan::PlayerEvent> events;
    const buridan::SteadyClock::time_point startTime = buridan::SteadyClock::now();
    player.start(protocol, startTime, &events);

    const std::chrono::duration<double> offsetSeconds(timeSeconds);
    const buridan::SteadyClock::duration offset =
        std::chrono::duration_cast<buridan::SteadyClock::duration>(offsetSeconds);
    const buridan::SteadyClock::time_point renderTime = startTime + offset;

    buridan::RingFrame frame;
    player.render(&frame, renderTime, &events);

    char timeText[32];
    std::snprintf(timeText, sizeof(timeText), "%.3f", timeSeconds);
    const std::string path = secondArgument + "_" + timeText + "s.ppm";
    if (!writePpm(frame, path)) {
      std::fprintf(stderr, "error: cannot write %s\n", path.c_str());
      return 1;
    }

    // Report which epoch is visible at that time.
    std::string epochDescription = "finished (blank)";
    if (player.isRunning()) {
      epochDescription = "epoch " + std::to_string(player.epochIndex()) + " (" + player.epochType() + ")";
    }
    std::printf("wrote %s  t=%.3fs  %s\n", path.c_str(), timeSeconds, epochDescription.c_str());
  }
  return 0;
}
