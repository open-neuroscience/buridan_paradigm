I have build a Buridan Arena to track behaviour of insects. It consists of 6 LED panels, each with an array of RGB leds in a 64x64 arrangement. The panels are laid in a ring 1x6 arrangement, with all panels facing inward. above the ring, in the central region, we have a USB camera pointing downward, and it records the behaviour of the insects inside the arena.

The panels are controlled by a Raspberry Pi 4 connection wise, the panels are connected to the Raspberry Pi as a 3 chain system with 2 panels in each chain. This is done so that we can achieve a high frame rate in the system. the hardware connections are so that chain 1 controls panels 1 and 2, chain 2 control panels 3 and 4, chain 3 control panels 5 and 6

The panels are driven using this [library](https://github.com/hzeller/rpi-rgb-led-matrix). I am connecting via SSH to the Pi (username: buridan password: buridan), and use the command line to start routines pre-recorded in the pi. 
Here is are examples for the library API use here https://github.com/hzeller/rpi-rgb-led-matrix/tree/master/examples-api-use for performance reasons, I would like to keep using C++.

Now, as the next step for this project, I want to use [Bonsai-RX](https://bonsai-rx.org/) (a reactive, visual programming environment coded in C#) to create a system that researchers can use to control the Buridan arena. The reason is that Bonsai-RX is open source and has most elements in place to make a system that drives the panels, while gets the camera feed and processes the images to track the insect.

 -Bonsai-RX core code base is here: https://github.com/bonsai-rx/bonsai
 -Bonsai has an extension implementing zeromq functionality, which we can use for the network connection with the Pi: https://github.com/bonsai-rx/zeromq
 -Bonsai has an extendion called [BonVision](https://bonvision.github.io/#) that is used to create and control visual environments. I would like to test it for stimulus generation. the codebase is here: https://github.com/bonvision/BonVision/tree/main/BonVision
 -Bonsai has an extension implementing GUI creation capabilities, which we can use to create the GUI on Bonsai: https://github.com/bonsai-rx/gui
 
  
Requirements:
- Use Bonsai-RX latest release 2.9.1
- All code should be throughly commented
- When using the rpi-rgb-led-matrix library, use C++ to keep performance. 
 
Guidelines:
- Explicit is better than implicit
- Whenever possible, due one operation per code line

 
 GUI Requirements:
 - Display the raw camera feed
 - Display the processed version of the camera feed, which will be the raw feed converted to the grayscale, then passed throught a threshold to result in frames showing binary data, where the animal should be white, and the rest black
 - Display a place for users to decide on which folder on the windows PC the experimental data will be saved. 
 - Display a place for users to select which protocol/stimulus they want to display to the animal. including a button to start the protocol (which should automatically start the camera feed and recording of the video to the folder in the item above).
 - Display a place for users to create a protocol/stimulus and save it to the windows computer as well as the raspberry pi.
 - Display information about the network connection to the Pi, including a place to type the username (default should be: buridan) and the password (default should be: buridan)

---

## Project structure (implementation)

| Folder | Content |
|---|---|
| `pi/` | `buridan_server` (C++, rpi-rgb-led-matrix + ZeroMQ): renders protocols on the 6-panel ring, accepts commands, streamed frames, publishes status/events. Setup: `pi/scripts/setup_pi.sh`. |
| `bonsai/` | `BuridanArena.bonsai` (GUI workflow), the Bonsai 2.9.1 environment (`.bonsai/Setup.cmd`), and the `Buridan.Bonsai` package (C#, `src/`) with the operators used by the workflow. |
| `docs/` | [Getting started](docs/getting-started.md) and the [protocol / network / data file reference](docs/protocol-schema.md). |
