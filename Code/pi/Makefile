# =============================================================================
# Makefile for buridan_server (Raspberry Pi side of the Buridan arena)
#
# Prerequisites (installed by scripts/setup_pi.sh):
#   - rpi-rgb-led-matrix cloned and built, location given by RGB_LIB_DISTRIBUTION
#   - libzmq3-dev, cppzmq-dev, nlohmann-json3-dev
#
# Targets:
#   make                 build bin/buridan_server
#   make preview         build bin/render_preview (no LED library needed)
#   make install         copy binary, example protocols and arena config to
#                        $(PREFIX) (default: ~/buridan)
#   make clean           remove build output
# =============================================================================

# Location of the rpi-rgb-led-matrix checkout.
RGB_LIB_DISTRIBUTION ?= $(HOME)/rpi-rgb-led-matrix
RGB_INCDIR = $(RGB_LIB_DISTRIBUTION)/include
RGB_LIBDIR = $(RGB_LIB_DISTRIBUTION)/lib
RGB_LIBRARY = $(RGB_LIBDIR)/librgbmatrix.a

# Where 'make install' puts everything.
PREFIX ?= $(HOME)/buridan

CXX ?= g++
CXXFLAGS ?= -O3 -Wall -Wextra
override CXXFLAGS += -std=c++17
# -isystem hides warnings coming from the library headers.
CPPFLAGS += -isystem $(RGB_INCDIR)

BUILD_DIR = build
BIN_DIR = bin

# Sources that do not depend on the LED library or ZeroMQ.
CORE_SOURCES = \
	src/ArenaConfig.cpp \
	src/BarRegionsStimulus.cpp \
	src/GratingStimulus.cpp \
	src/Protocol.cpp \
	src/ProtocolPlayer.cpp \
	src/RingFrame.cpp \
	src/Stimulus.cpp \
	src/StripesStimulus.cpp \
	src/TestPattern.cpp

# Sources of the server only.
SERVER_SOURCES = \
	src/CommandServer.cpp \
	src/FrameReceiver.cpp \
	src/RingMapper.cpp \
	src/ServerState.cpp \
	src/StatusPublisher.cpp \
	src/main.cpp

CORE_OBJECTS = $(patsubst src/%.cpp,$(BUILD_DIR)/%.o,$(CORE_SOURCES))
SERVER_OBJECTS = $(patsubst src/%.cpp,$(BUILD_DIR)/%.o,$(SERVER_SOURCES))
PREVIEW_OBJECTS = $(BUILD_DIR)/render_preview.o

SERVER_LDLIBS = -L$(RGB_LIBDIR) -lrgbmatrix -lzmq -lrt -lm -lpthread

.PHONY: all preview install clean

all: $(BIN_DIR)/buridan_server

preview: $(BIN_DIR)/render_preview

$(BIN_DIR)/buridan_server: $(CORE_OBJECTS) $(SERVER_OBJECTS) $(RGB_LIBRARY)
	@mkdir -p $(BIN_DIR)
	$(CXX) $(CXXFLAGS) $(CORE_OBJECTS) $(SERVER_OBJECTS) -o $@ $(LDFLAGS) $(SERVER_LDLIBS)

$(BIN_DIR)/render_preview: $(CORE_OBJECTS) $(PREVIEW_OBJECTS)
	@mkdir -p $(BIN_DIR)
	$(CXX) $(CXXFLAGS) $(CORE_OBJECTS) $(PREVIEW_OBJECTS) -o $@ $(LDFLAGS)

# Build the LED library if it has not been built yet.
$(RGB_LIBRARY):
	$(MAKE) -C $(RGB_LIBDIR)

$(BUILD_DIR)/%.o: src/%.cpp
	@mkdir -p $(BUILD_DIR)
	$(CXX) $(CPPFLAGS) $(CXXFLAGS) -MMD -MP -c $< -o $@

$(BUILD_DIR)/render_preview.o: tools/render_preview.cpp
	@mkdir -p $(BUILD_DIR)
	$(CXX) $(CPPFLAGS) -Isrc $(CXXFLAGS) -MMD -MP -c $< -o $@

install: $(BIN_DIR)/buridan_server
	mkdir -p $(PREFIX)/bin $(PREFIX)/protocols
	install -m 755 $(BIN_DIR)/buridan_server $(PREFIX)/bin/buridan_server
	cp -n protocols/*.json $(PREFIX)/protocols/ || true
	cp -n arena_config.json $(PREFIX)/arena_config.json || true

clean:
	rm -rf $(BUILD_DIR) $(BIN_DIR)

-include $(wildcard $(BUILD_DIR)/*.d)
