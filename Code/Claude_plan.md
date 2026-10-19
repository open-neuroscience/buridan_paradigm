Buridan Arena: Bonsai-RX control system (full-system plan)

 Context

 The arena is a ring of 6 × (64×64 RGB) LED panels driven by a Raspberry Pi 4 (3 parallel chains × 2 panels,
 hzeller/rpi-rgb-led-matrix), with a top-down USB camera on the Windows PC. Today routines are started by hand over
 SSH. Goal (README.md): a Bonsai-RX 2.9.1 system researchers can use to pick/create protocols, drive the panels, view
 raw + thresholded camera feed, record video/tracking to a chosen folder, and see the Pi connection status.

 Decisions made with the user:
 - Hybrid stimulus model: protocols are JSON files stored on both PC and Pi; the Pi renders standard stimuli locally
   in C++ at full frame rate; the PC can additionally stream BonVision-rendered frames over ZeroMQ for custom
   stimuli.
 - Adapter board: Electrodragon-style → hardware_mapping = "regular" (supports 3 parallel chains on a Pi 4).
 - Initial stimulus types: classic Buridan stripes and rotating gratings.
 - Code rules: thorough comments, explicit over implicit, one operation per line, C++ on the Pi.

 The working directory currently contains only README.md (greenfield, not a git repo — suggest git init).

 Architecture

  Windows PC (Bonsai 2.9.1)                               Raspberry Pi 4
  ┌───────────────────────────────┐   SSH (user/pass)    ┌──────────────────────────────┐
  │ GUI (Bonsai.Gui)              │ ──────────────────▶ │ starts buridan_server (sudo) │
  │ Camera → Gray → Threshold     │                      │                              │
  │ Tracking → CSV, VideoWriter   │  ZMQ REQ/REP :5555   │ Command thread (JSON)        │
  │ Protocol editor / selector    │ ◀────────────────▶ │ Protocol player + stimuli    │
  │ BonVision stream (phase 4)    │  ZMQ PUSH→PULL :5556 │ Streamed-frame receiver      │
  │ Status / sync logger          │  ZMQ SUB←PUB  :5557  │ Status/event publisher       │
  └───────────────────────────────┘                      │ Render loop → RingMapper →   │
                                                         │ FrameCanvas → SwapOnVSync    │
                                                         └──────────────────────────────┘

 Proposed repository layout

 buridan_bonsai1/
   README.md
   pi/                         # C++ server for the Raspberry Pi
     CMakeLists.txt  (or Makefile linking rpi-rgb-led-matrix/lib/librgbmatrix.a)
     src/main.cpp              # flag parsing, matrix creation, thread start-up
     src/RingMapper.{h,cpp}    # 384×64 ring coords → 128×192 library canvas
     src/RingFrame.{h,cpp}     # plain RGB buffer 384×64 that stimuli draw into
     src/Stimulus.h            # abstract render(RingFrame&, double t_seconds)
     src/StripesStimulus.{h,cpp}
     src/GratingStimulus.{h,cpp}
     src/Protocol.{h,cpp}      # JSON load/save, epoch list
     src/ProtocolPlayer.{h,cpp}
     src/CommandServer.{h,cpp} # ZMQ REP, JSON commands
     src/FrameReceiver.{h,cpp} # ZMQ PULL, streamed frames
     src/StatusPublisher.{h,cpp}
     protocols/                # example JSON protocols
     scripts/setup_pi.sh       # apt deps, disable audio, build
   bonsai/
     BuridanArena.bonsai       # main workflow
     Extensions/               # Bonsai scripting extensions (C#), compiled by Bonsai
       Extensions.csproj       # package refs: SSH.NET, Newtonsoft.Json
       ProtocolDefinition.cs ...
     Bonsai.config / packages  # Bonsai 2.9.1 environment (bootstrap via Setup.ps1)
   protocols/                  # shared example protocols (same schema as Pi)
   docs/protocol-schema.md     # JSON schema + command reference

 Component 1 — Pi C++ server (pi/)

 Dependencies (apt): libzmq3-dev, cppzmq-dev (header zmq.hpp), nlohmann-json3-dev; rpi-rgb-led-matrix cloned + built
 (make -C lib).

 Matrix setup (main.cpp): RGBMatrix::Options with rows=64, cols=64, chain_length=2, parallel=3,
 hardware_mapping="regular"; RuntimeOptions.gpio_slowdown default 2 (tune 2–4 on Pi 4). Use ParseOptionsFromFlags so
 every --led-* flag remains overridable. Draw via CreateFrameCanvas() + SwapOnVSync() (double-buffering, as in
 examples-api-use).

 RingMapper: the library canvas is cols×chain = 128 wide and rows×parallel = 192 tall (each parallel chain is a
 64-px-high band). For ring pixel (x, y) with x ∈ [0,384):
 panel      = x / 64            // 0..5  (panel 1..6 in README)
 chain      = panel / 2         // 0..2
 posInChain = panel % 2         // 0..1
 localX     = x % 64
 canvasX    = posInChain * 64 + localX
 canvasY    = chain * 64 + y
 Plus per-panel flipX/flipY config (JSON/flags) to correct physical mounting. A --test-pattern mode draws each
 panel's number and a colour gradient to verify order/orientation.

 Stimuli (angle units: 384 px = 360°, so 1 px ≈ 0.9375°):
 - StripesStimulus: N stripes (default 2, 180° apart), params centers_deg[], width_deg, stripe_rgb, background_rgb,
   optional height_px.
 - GratingStimulus: period_deg, speed_deg_s, direction (cw/ccw), waveform (square/sine), duty_cycle, rgb_a, rgb_b.

 Protocol JSON (identical schema on PC and Pi; documented in docs/protocol-schema.md):
 { "name": "stripes_then_grating", "schema_version": 1, "frame_rate_hz": 60,
   "epochs": [
     { "type": "stripes", "duration_s": 300, "params": { "centers_deg": [0,180], "width_deg": 11.25 } },
     { "type": "grating", "duration_s": 60,  "params": { "period_deg": 30, "speed_deg_s": 45, "direction": "cw",
 "waveform": "square" } } ] }
 Stored in ~/buridan/protocols/<name>.json.

 Threads:
 1. Render loop — fixed-rate; asks the active source (ProtocolPlayer, streamed frame, or blank) for a RingFrame, maps
    to canvas, SwapOnVSync; increments a frame counter.
 2. CommandServer — ZMQ REP tcp://*:5555. JSON commands, JSON replies {"ok":bool,...}: ping, status, list_protocols,
    upload_protocol {name, protocol}, delete_protocol, start_protocol {name}, stop, set_mode
    {"protocol"|"stream"|"blank"}, test_pattern.
 3. FrameReceiver — ZMQ PULL tcp://*:5556, accepts raw 384×64×3 RGB (73,728 B) frames; latest-frame-wins buffer.
 4. StatusPublisher — ZMQ PUB tcp://*:5557: periodic {frame, t_pi, mode, protocol, epoch} and event messages
    (epoch_start, protocol_end) for synchronisation logs.
    Shared state guarded by std::mutex; all one-operation-per-line, heavily commented.

 Pi setup (scripts/setup_pi.sh): install deps, blacklist snd_bcm2835 + dtparam=audio=off (library refuses to run
 otherwise), optional isolcpus=3, build. Server must start as root (library drops privileges after init).

 Component 2 — Bonsai extensions (bonsai/Extensions/, C#)

 Bonsai compiles these automatically when the workflow opens. Each class/operator thoroughly commented.
 - ProtocolDefinition, Epoch, StripesParameters, GratingParameters — C# mirror of the JSON schema with [Description]
   attributes so they are editable in a PropertyGrid (collection editor for epochs).
 - ExperimentSettings — DataFolder with [Editor("Bonsai.Design.FolderNameEditor, Bonsai.Design", ...)] (gives the
   folder-browse button in a PropertyGrid; Bonsai.Gui has no dedicated folder picker), AnimalId, Notes.
 - ConnectionSettings — Host, Username = "buridan", Password = "buridan" ([PasswordPropertyText]), ports.
 - StartPiServer (Sink/Transform) — uses SSH.NET to connect with the GUI credentials and run echo <pw> | sudo -S
   ~/buridan/bin/buridan_server <flags> in background; emits connection status.
 - CreateCommand — builds the JSON command string for a Request node; ParseReply — JSON → typed reply.
 - SaveProtocolLocal / LoadProtocolList — read/write <protocols folder>/*.json; list feeds a ComboBox data source.
 - RingFrameToBytes — IplImage (384×64, BGR) → RGB byte[] for streaming (phase 4).
 - ExperimentPaths — builds <DataFolder>/<AnimalId>_<yyyyMMdd_HHmmss>/ with video.avi, tracking.csv, events.csv,
   protocol.json copy.

 Component 3 — Bonsai workflow (bonsai/BuridanArena.bonsai)

 Packages: Bonsai.Core/Editor 2.9.1, Bonsai.Vision (+Design), Bonsai.ZeroMQ, Bonsai.Gui, BonVision (phase 4).

 - Camera: CameraCapture → Publish subject RawFrame. Processing: Grayscale → Threshold (threshold value bound to a
   GUI Slider; ThresholdType chosen so the animal is white — BinaryInv if the animal is dark on bright LEDs) →
   subject BinaryFrame. Tracking: FindContours → BinaryRegionAnalysis → LargestBinaryRegion → centroid/orientation →
   CsvWriter.
 - Run control: Start button → upload(if needed) + start_protocol request → triggers recording window
   (SubscribeWhen/TakeUntil Stop or protocol_end event). Recording: VideoWriter on RawFrame + CsvWriter (camera
   timestamps, tracking, Pi status/events). Camera preview stays live before Start so thresholds can be tuned;
   recording begins with Start.
 - Network: Request → tcp://<host>:5555; heartbeat Timer (1 s) sends ping → connection-status label (connected / last
   RTT / mode / active protocol). Subscriber on :5557 for status/events.
 - GUI (TabControl + TableLayoutPanel, mapped via VisualizerMapping):
   - Run tab: raw feed + binary feed image visualizers, ExperimentSettings PropertyGrid (folder picker), protocol
     ComboBox, threshold Slider, Start/Stop buttons, status label.
   - Protocol editor tab: ProtocolDefinition PropertyGrid, name TextBox, “Save” button → SaveProtocolLocal and
     upload_protocol to Pi.
   - Connection tab: Host / Username (default buridan) / Password (default buridan) TextBoxes, Connect button (→
     StartPiServer), connection status.

 Component 4 — BonVision streaming (phase 4, spike first)

 BonVision renders a 360° cylinder view into a 384×64 offscreen target; a small custom operator reads back pixels
 (GL.ReadPixels via OpenTK, available through Bonsai.Shaders) → RingFrameToBytes → Push to :5556 after sending
       → subject BinaryFrame. Tracking: FindContours → BinaryRegionAnalysis → LargestBinaryRegion → centroid/orientation → CsvWriter.
     - Run control: Start button → upload(if needed) + start_protocol request → triggers recording window (SubscribeWhen/TakeUntil Stop or protocol_end event). Recording: VideoWriter on RawFrame + CsvWriter (camera
       timestamps, tracking, Pi status/events). Camera preview stays live before Start so thresholds can be tuned; recording begins with Start.
     - Network: Request → tcp://<host>:5555; heartbeat Timer (1 s) sends ping → connection-status label (connected / last RTT / mode / active protocol). Subscriber on :5557 for status/events.
     - GUI (TabControl + TableLayoutPanel, mapped via VisualizerMapping):
       - Run tab: raw feed + binary feed image visualizers, ExperimentSettings PropertyGrid (folder picker), protocol ComboBox, threshold Slider, Start/Stop buttons, status label.
       - Protocol editor tab: ProtocolDefinition PropertyGrid, name TextBox, “Save” button → SaveProtocolLocal and upload_protocol to Pi.
       - Connection tab: Host / Username (default buridan) / Password (default buridan) TextBoxes, Connect button (→ StartPiServer), connection status.

     Component 4 — BonVision streaming (phase 4, spike first)

     BonVision renders a 360° cylinder view into a 384×64 offscreen target; a small custom operator reads back pixels (GL.ReadPixels via OpenTK, available through Bonsai.Shaders) → RingFrameToBytes → Push to :5556 after
     sending set_mode stream. Risk: Bonsai.Shaders readback is not a documented built-in — validate feasibility and achievable rate before building UI around it. Fallback: generate frames with Bonsai.Vision drawing operators.

     Implementation phases

     1. Pi server standalone: matrix + RingMapper + test pattern; stripes/grating from a local --protocol file.json flag (no network).
     2. Pi network interface: CommandServer, StatusPublisher, FrameReceiver.
     3. Bonsai extensions + workflow + GUI (camera/threshold/recording first, then connection, then protocol editor).
     4. BonVision streaming spike → integration.
     5. Docs: update README with setup/usage; docs/protocol-schema.md.

     Verification

     - Phase 1 (on Pi): sudo ./buridan_server --test-pattern --led-show-refresh → panels 1–6 show correct numbers/orientation around the ring, refresh rate stable; play example stripes and grating protocols; check grating
       moves at the configured °/s (stopwatch over one revolution).
     - Phase 2: from the PC, a minimal Bonsai workflow (or pyzmq one-liner) sends ping, list_protocols, upload_protocol, start_protocol, stop; confirm replies and :5557 events; stream a test frame to :5556.
     - Phase 3: open BuridanArena.bonsai in Bonsai 2.9.1 → extensions compile; Connect starts the Pi server over SSH and status turns “connected”; raw + binary feeds display, animal appears white; Save protocol creates JSON
       locally and on Pi (ls ~/buridan/protocols); Start creates the session folder with video, tracking CSV, events CSV, protocol copy; Stop/protocol_end closes files cleanly.
     - Phase 4: BonVision stimulus visible on the ring; measure end-to-end frame rate via Pi status frame counter vs. sent frames.

     Notes / open risks

     - Plain-text default credentials (buridan/buridan) are a requirement; recommend switching to SSH keys later and keeping the Pi on an isolated network.
     - Embedding Bonsai.Vision image visualizers inside Bonsai.Gui containers relies on VisualizerMapping (mashup visualizers) — verify early in phase 3; fallback is separate visualizer windows.
     - Exact Bonsai.ZeroMQ / Bonsai.Gui versions compatible with 2.9.1 to be pinned when creating the Bonsai environment.