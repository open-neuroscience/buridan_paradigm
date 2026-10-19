// =============================================================================
// main.cpp — buridan_server
//
// Drives the 6-panel LED ring of the Buridan arena and accepts commands from
// the Bonsai control system on the PC.
//
// Threads:
//   main thread     : render loop (draws one frame per display period)
//   command thread  : ZeroMQ REP, JSON commands        (default port 5555)
//   frame thread    : ZeroMQ PULL, streamed frames     (default port 5556)
//   status thread   : ZeroMQ PUB, status + events      (default port 5557)
//
// Typical use (must be started as root; privileges are dropped afterwards):
//   sudo ./buridan_server
//   sudo ./buridan_server --test-pattern --led-show-refresh
//   sudo ./buridan_server --protocol=protocols/classic_buridan.json
//
// Root privileges are dropped to the user that ran sudo (or "buridan") once
// the hardware is initialised; override with --led-drop-priv-user=NAME.
//
// All rpi-rgb-led-matrix flags (--led-*) are accepted and override the
// defaults set below. Run with --help for the full list.
// =============================================================================
#include <atomic>
#include <chrono>
#include <csignal>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

#include <led-matrix.h>
#include <zmq.hpp>

#include "ArenaConfig.h"
#include "CommandServer.h"
#include "FrameReceiver.h"
#include "Protocol.h"
#include "ProtocolPlayer.h"
#include "RingFrame.h"
#include "RingMapper.h"
#include "ServerState.h"
#include "StatusPublisher.h"
#include "TestPattern.h"

namespace {

using buridan::SteadyClock;

// Set by the signal handler when Ctrl+C or SIGTERM is received.
// std::atomic<bool> is lock-free, so it is safe to write from a signal handler.
std::atomic<bool> gSignalReceived{false};

void handleSignal(int /*signalNumber*/) {
  gSignalReceived.store(true);
}

// Returns the user that ran sudo, or "buridan" if that is unknown.
std::string defaultExperimentUser() {
  const char* sudoUser = std::getenv("SUDO_USER");
  if (sudoUser != nullptr && std::strlen(sudoUser) > 0) {
    return std::string(sudoUser);
  }
  return std::string("buridan");
}

// Options specific to buridan_server (the --led-* options are handled by the
// rpi-rgb-led-matrix library).
struct ServerOptions {
  // User that owns the default protocol / config folders.
  std::string runAsUser;
  std::string protocolFolder;
  std::string arenaConfigPath;
  int commandPort = 5555;
  int framePort = 5556;
  int statusPort = 5557;
  double idleFrameRateHz = 60.0;
  double statusRateHz = 10.0;
  bool startWithTestPattern = false;
  std::string startupProtocolPath;
  bool noHardware = false;
  bool showHelp = false;
};

void printUsage(const char* programName) {
  std::fprintf(stderr,
               "Usage: sudo %s [options] [--led-* options]\n"
               "\n"
               "Options:\n"
               "  --protocol-dir=PATH      folder with protocol files (default: /home/<user>/buridan/protocols)\n"
               "  --arena-config=PATH      arena configuration file (default: /home/<user>/buridan/arena_config.json)\n"
               "  --command-port=N         ZeroMQ command port (default: 5555)\n"
               "  --frame-port=N           ZeroMQ streamed-frame port (default: 5556)\n"
               "  --status-port=N          ZeroMQ status/event port (default: 5557)\n"
               "  --frame-rate=HZ          frame rate when no protocol is playing (default: 60)\n"
               "  --status-rate=HZ         status messages per second (default: 10)\n"
               "  --test-pattern           start by showing the panel test pattern\n"
               "  --protocol=FILE          start by playing this protocol file\n"
               "  --no-hardware            run without LED panels (for testing the network side)\n"
               "  --help                   show this help\n"
               "\n",
               programName);
  rgb_matrix::PrintMatrixFlags(stderr);
}

// Returns true and stores the text after '=' if 'argument' starts with 'prefix'.
bool readFlagValue(const std::string& argument, const std::string& prefix, std::string* value) {
  if (argument.compare(0, prefix.size(), prefix) != 0) {
    return false;
  }
  *value = argument.substr(prefix.size());
  return true;
}

// Parses buridan_server's own flags. Returns false with a message on error.
bool parseServerOptions(int argc, char** argv, ServerOptions* options, std::string* errorMessage) {
  for (int index = 1; index < argc; ++index) {
    const std::string argument = argv[index];
    std::string value;

    if (argument == "--help" || argument == "-h") {
      options->showHelp = true;
    } else if (argument == "--test-pattern") {
      options->startWithTestPattern = true;
    } else if (argument == "--no-hardware") {
      options->noHardware = true;
    } else if (readFlagValue(argument, "--protocol-dir=", &value)) {
      options->protocolFolder = value;
    } else if (readFlagValue(argument, "--arena-config=", &value)) {
      options->arenaConfigPath = value;
    } else if (readFlagValue(argument, "--command-port=", &value)) {
      options->commandPort = std::atoi(value.c_str());
    } else if (readFlagValue(argument, "--frame-port=", &value)) {
      options->framePort = std::atoi(value.c_str());
    } else if (readFlagValue(argument, "--status-port=", &value)) {
      options->statusPort = std::atoi(value.c_str());
    } else if (readFlagValue(argument, "--frame-rate=", &value)) {
      options->idleFrameRateHz = std::atof(value.c_str());
    } else if (readFlagValue(argument, "--status-rate=", &value)) {
      options->statusRateHz = std::atof(value.c_str());
    } else if (readFlagValue(argument, "--protocol=", &value)) {
      options->startupProtocolPath = value;
    } else {
      *errorMessage = "unknown option: " + argument;
      return false;
    }
  }

  // Default paths live in the home folder of that user.
  const std::string baseFolder = "/home/" + options->runAsUser + "/buridan";
  if (options->protocolFolder.empty()) {
    options->protocolFolder = baseFolder + "/protocols";
  }
  if (options->arenaConfigPath.empty()) {
    options->arenaConfigPath = baseFolder + "/arena_config.json";
  }

  // Sanity checks.
  const bool portsValid = (options->commandPort > 0) && (options->framePort > 0) && (options->statusPort > 0);
  if (!portsValid) {
    *errorMessage = "ports must be positive integers";
    return false;
  }
  const bool frameRateValid = (options->idleFrameRateHz >= buridan::kMinFrameRateHz) &&
                              (options->idleFrameRateHz <= buridan::kMaxFrameRateHz);
  if (!frameRateValid) {
    *errorMessage = "--frame-rate must be between 1 and 240";
    return false;
  }
  if (options->statusRateHz <= 0.0) {
    *errorMessage = "--status-rate must be positive";
    return false;
  }
  return true;
}

// Converts a PlayerEvent into the JSON event published to the PC.
nlohmann::json playerEventToJson(const buridan::PlayerEvent& playerEvent, uint64_t frameCount) {
  nlohmann::json event = nlohmann::json::object();
  event["event"] = playerEvent.kind;
  event["protocol"] = playerEvent.protocolName;
  event["epoch"] = playerEvent.epochIndex;
  event["epoch_type"] = playerEvent.epochType;
  // Frame number on which this event first becomes visible.
  event["frame"] = frameCount;
  return event;
}

// The render loop: runs on the main thread until a stop is requested.
// 'matrix' is nullptr when running with --no-hardware.
// 'initialPwmBits' is the bit depth set by --led-pwm-bits (default 11).
// 'pwmLsbNanoseconds' is set by --led-pwm-lsb-nanoseconds (default 130); it
// can only be chosen at startup and is reported in the status.
void runRenderLoop(rgb_matrix::RGBMatrix* matrix,
                   const buridan::ArenaConfig& arenaConfig,
                   const ServerOptions& options,
                   int initialPwmBits,
                   int pwmLsbNanoseconds,
                   buridan::ServerState* state) {
  const buridan::RingMapper mapper(arenaConfig);
  buridan::ProtocolPlayer player(arenaConfig);
  buridan::RingFrame frame;
  buridan::DisplayMode mode = buridan::DisplayMode::Blank;

  // Off-screen canvas for double buffering (hardware only).
  rgb_matrix::FrameCanvas* offscreenCanvas = nullptr;
  if (matrix != nullptr) {
    offscreenCanvas = matrix->CreateFrameCanvas();
  }

  // Colour bit depth currently used by the panels.
  int pwmBits = initialPwmBits;

  uint64_t frameCount = 0;
  double measuredFrameRateHz = 0.0;
  uint64_t framesAtLastMeasurement = 0;
  SteadyClock::time_point lastMeasurementTime = SteadyClock::now();
  SteadyClock::time_point nextFrameTime = SteadyClock::now();

  while (!state->isStopRequested()) {
    // Ctrl+C / SIGTERM: tell all threads to stop.
    if (gSignalReceived.load()) {
      state->requestStop();
      break;
    }

    const SteadyClock::time_point now = SteadyClock::now();
    std::vector<buridan::PlayerEvent> playerEvents;

    // --- 1) apply requests from the command thread ---------------------------
    const std::vector<buridan::DisplayRequest> requests = state->takeRequests();
    for (const buridan::DisplayRequest& request : requests) {
      // Anything that replaces a running protocol stops it first.
      if (player.isRunning()) {
        nlohmann::json stoppedEvent = nlohmann::json::object();
        stoppedEvent["event"] = "protocol_stopped";
        stoppedEvent["protocol"] = player.protocolName();
        stoppedEvent["epoch"] = player.epochIndex();
        stoppedEvent["frame"] = frameCount;
        state->pushEvent(stoppedEvent);
        player.stop();
      }

      mode = request.mode;
      if (mode == buridan::DisplayMode::Protocol) {
        player.start(request.protocol, now, &playerEvents);
      }

      nlohmann::json modeEvent = nlohmann::json::object();
      modeEvent["event"] = "mode_changed";
      modeEvent["mode"] = buridan::displayModeToString(mode);
      modeEvent["frame"] = frameCount;
      state->pushEvent(modeEvent);
    }

    // A new PWM bit depth only changes how the panels are refreshed, so it
    // does not interrupt the protocol or stream that is playing.
    const int requestedPwmBits = state->takePwmBitsRequest();
    if (requestedPwmBits != 0) {
      if (matrix != nullptr) {
        // The setting is stored per canvas: matrix->SetPWMBits() changes the
        // canvas on screen (and future canvases), so the off-screen canvas
        // must be changed as well.
        const bool activeChanged = matrix->SetPWMBits(static_cast<uint8_t>(requestedPwmBits));
        const bool offscreenChanged = offscreenCanvas->SetPWMBits(static_cast<uint8_t>(requestedPwmBits));
        if (activeChanged && offscreenChanged) {
          pwmBits = requestedPwmBits;
        }
      } else {
        // --no-hardware: remember the value so the network side can be tested.
        pwmBits = requestedPwmBits;
      }

      nlohmann::json pwmEvent = nlohmann::json::object();
      pwmEvent["event"] = "pwm_bits_changed";
      pwmEvent["pwm_bits"] = pwmBits;
      pwmEvent["frame"] = frameCount;
      state->pushEvent(pwmEvent);

      // Restart the frame rate measurement so it only covers the new setting.
      framesAtLastMeasurement = frameCount;
      lastMeasurementTime = now;
      measuredFrameRateHz = 0.0;
    }

    // --- 2) draw the frame for the current mode ------------------------------
    if (mode == buridan::DisplayMode::Protocol) {
      player.render(&frame, now, &playerEvents);
      // When the protocol has ended, go back to a blank display.
      if (!player.isRunning()) {
        mode = buridan::DisplayMode::Blank;
      }
    } else if (mode == buridan::DisplayMode::Stream) {
      const bool hasFrame = state->copyLatestStreamedFrame(&frame);
      if (!hasFrame) {
        const buridan::Rgb black;
        frame.fill(black);
      }
    } else if (mode == buridan::DisplayMode::TestPattern) {
      buridan::drawTestPattern(&frame);
    } else {
      const buridan::Rgb black;
      frame.fill(black);
    }

    // --- 3) send the frame to the panels --------------------------------------
    if (matrix != nullptr) {
      mapper.draw(frame, offscreenCanvas);
      // Swap at the next panel refresh; returns the old canvas for reuse.
      offscreenCanvas = matrix->SwapOnVSync(offscreenCanvas);
    }
    frameCount += 1;

    // --- 4) publish what happened on this frame -------------------------------
    for (const buridan::PlayerEvent& playerEvent : playerEvents) {
      const nlohmann::json event = playerEventToJson(playerEvent, frameCount);
      state->pushEvent(event);
    }

    // Measure the real frame rate once per second.
    const SteadyClock::time_point afterDraw = SteadyClock::now();
    const std::chrono::duration<double> sinceMeasurement = afterDraw - lastMeasurementTime;
    if (sinceMeasurement.count() >= 1.0) {
      const uint64_t framesDrawn = frameCount - framesAtLastMeasurement;
      measuredFrameRateHz = static_cast<double>(framesDrawn) / sinceMeasurement.count();
      framesAtLastMeasurement = frameCount;
      lastMeasurementTime = afterDraw;
    }

    buridan::StatusSnapshot snapshot;
    snapshot.mode = mode;
    snapshot.frameCount = frameCount;
    snapshot.measuredFrameRateHz = measuredFrameRateHz;
    snapshot.protocolName = player.protocolName();
    snapshot.epochIndex = player.epochIndex();
    snapshot.epochType = player.epochType();
    snapshot.pwmBits = pwmBits;
    snapshot.pwmLsbNanoseconds = pwmLsbNanoseconds;
    snapshot.hardwareConnected = (matrix != nullptr);
    state->updateStatus(snapshot);

    // --- 5) wait until the next frame is due ----------------------------------
    double frameRateHz = options.idleFrameRateHz;
    if (mode == buridan::DisplayMode::Protocol) {
      frameRateHz = player.frameRateHz();
    }
    const std::chrono::duration<double> framePeriodSeconds(1.0 / frameRateHz);
    const SteadyClock::duration framePeriod = std::chrono::duration_cast<SteadyClock::duration>(framePeriodSeconds);
    nextFrameTime += framePeriod;
    // If we are late (e.g. after a slow frame), restart the schedule from now
    // instead of rushing several frames to catch up.
    if (nextFrameTime < afterDraw) {
      nextFrameTime = afterDraw;
    }
    std::this_thread::sleep_until(nextFrameTime);
  }

  // Leave the panels dark on exit.
  if (matrix != nullptr) {
    matrix->Clear();
  }
}

}  // namespace

int main(int argc, char** argv) {
  // --- LED matrix defaults for the Buridan arena ------------------------------
  // 6 panels of 64x64: 3 parallel chains, 2 panels per chain.
  rgb_matrix::RGBMatrix::Options matrixOptions;
  matrixOptions.rows = buridan::kPanelRows;
  matrixOptions.cols = buridan::kPanelCols;
  matrixOptions.chain_length = buridan::kPanelsPerChain;
  matrixOptions.parallel = buridan::kParallelChains;
  matrixOptions.hardware_mapping = "regular";

  rgb_matrix::RuntimeOptions runtimeOptions;
  // The Pi 4 is fast; panels usually need some GPIO slowdown. Tune 2..4.
  runtimeOptions.gpio_slowdown = 2;
  // Run in the foreground. With daemon >= 0 the library starts its refresh
  // thread while still root, so it keeps its real-time priority.
  runtimeOptions.daemon = 0;
  // Drop root privileges after the hardware is initialised...
  runtimeOptions.drop_privileges = 1;
  // ...to the experiment user (not the library default "daemon"), so that
  // protocol files can be written to that user's home folder.
  // The string must stay alive until the matrix is created.
  const std::string defaultUser = defaultExperimentUser();
  runtimeOptions.drop_priv_user = defaultUser.c_str();
  runtimeOptions.drop_priv_group = defaultUser.c_str();

  // The library removes the --led-* flags it understands from argv.
  const bool ledFlagsValid = rgb_matrix::ParseOptionsFromFlags(&argc, &argv, &matrixOptions, &runtimeOptions);
  if (!ledFlagsValid) {
    printUsage(argv[0]);
    return 1;
  }

  // --- our own flags ----------------------------------------------------------
  ServerOptions options;
  // Default folders belong to the user we drop privileges to.
  options.runAsUser = runtimeOptions.drop_priv_user;
  std::string errorMessage;
  const bool serverFlagsValid = parseServerOptions(argc, argv, &options, &errorMessage);
  if (!serverFlagsValid) {
    std::fprintf(stderr, "error: %s\n\n", errorMessage.c_str());
    printUsage(argv[0]);
    return 1;
  }
  if (options.showHelp) {
    printUsage(argv[0]);
    return 0;
  }

  // --- arena configuration ------------------------------------------------------
  buridan::ArenaConfig arenaConfig;
  const bool arenaConfigLoaded = buridan::loadArenaConfig(options.arenaConfigPath, &arenaConfig, &errorMessage);
  if (!arenaConfigLoaded) {
    std::fprintf(stderr, "warning: %s\n", errorMessage.c_str());
    std::fprintf(stderr, "warning: using default arena configuration (panels in order, no flips)\n");
    arenaConfig = buridan::ArenaConfig();
  }

  // --- startup protocol (validated before touching the hardware) ---------------
  buridan::ServerState state;
  if (!options.startupProtocolPath.empty()) {
    try {
      buridan::DisplayRequest request;
      request.mode = buridan::DisplayMode::Protocol;
      request.protocol = buridan::loadProtocolFile(options.startupProtocolPath, arenaConfig);
      state.pushRequest(request);
    } catch (const std::exception& error) {
      std::fprintf(stderr, "error: %s\n", error.what());
      return 1;
    }
  } else if (options.startWithTestPattern) {
    buridan::DisplayRequest request;
    request.mode = buridan::DisplayMode::TestPattern;
    state.pushRequest(request);
  }

  // --- LED matrix ---------------------------------------------------------------
  rgb_matrix::RGBMatrix* matrix = nullptr;
  if (!options.noHardware) {
    matrix = rgb_matrix::RGBMatrix::CreateFromOptions(matrixOptions, runtimeOptions);
    if (matrix == nullptr) {
      std::fprintf(stderr, "error: could not initialise the LED matrix (are you running with sudo?)\n");
      return 1;
    }
    std::fprintf(stderr, "[matrix] canvas %d x %d\n", matrix->width(), matrix->height());
  } else {
    std::fprintf(stderr, "[matrix] --no-hardware: frames are rendered but not displayed\n");
  }

  // --- signals ----------------------------------------------------------------
  std::signal(SIGINT, handleSignal);
  std::signal(SIGTERM, handleSignal);

  // --- network threads ----------------------------------------------------------
  zmq::context_t zmqContext(1);
  int exitCode = 0;
  {
    buridan::CommandServer commandServer(&zmqContext, options.commandPort, options.protocolFolder, arenaConfig, &state);
    buridan::FrameReceiver frameReceiver(&zmqContext, options.framePort, &state);
    buridan::StatusPublisher statusPublisher(&zmqContext, options.statusPort, options.statusRateHz, &state);

    // Each thread body catches its own errors (e.g. port already in use) and
    // stops the whole server, so a broken network never leaves a zombie.
    auto runSafely = [&state](const char* name, auto body) {
      try {
        body();
      } catch (const std::exception& error) {
        std::fprintf(stderr, "[%s] fatal error: %s\n", name, error.what());
        state.requestStop();
      }
    };

    std::thread commandThread([&]() { runSafely("command", [&]() { commandServer.run(); }); });
    std::thread frameThread([&]() { runSafely("frames", [&]() { frameReceiver.run(); }); });
    std::thread statusThread([&]() { runSafely("status", [&]() { statusPublisher.run(); }); });

    std::fprintf(stderr, "buridan_server %s ready (protocols: %s)\n", buridan::kServerVersion,
                 options.protocolFolder.c_str());

    try {
      runRenderLoop(matrix, arenaConfig, options, matrixOptions.pwm_bits, matrixOptions.pwm_lsb_nanoseconds,
                    &state);
    } catch (const std::exception& error) {
      std::fprintf(stderr, "[render] fatal error: %s\n", error.what());
      state.requestStop();
      exitCode = 1;
    }

    // Make sure every thread sees the stop request, then wait for them.
    state.requestStop();
    commandThread.join();
    frameThread.join();
    statusThread.join();
  }

  delete matrix;
  std::fprintf(stderr, "buridan_server stopped\n");
  return exitCode;
}
