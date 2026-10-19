# Getting started

This guide sets up the Raspberry Pi and the Windows PC, then runs a first
experiment. For file formats and the network interface, see
[protocol-schema.md](protocol-schema.md).

```
buridan_bonsai1/
├── pi/        C++ server for the Raspberry Pi (buridan_server)
├── bonsai/    Bonsai workflow, Bonsai environment and the Buridan.Bonsai package
└── docs/      this guide and the protocol / network reference
```

---

## 1. Raspberry Pi

**Hardware assumptions:** Raspberry Pi 4, a 3-chain adapter board using the
library's `regular` pin mapping (e.g. Electrodragon), and six 64 × 64 panels
(chain 1 = panels 1–2, chain 2 = panels 3–4, chain 3 = panels 5–6).

1. Copy the `pi/` folder to the Pi (for example to `~/buridan_bonsai1/pi`).
2. Run the setup script **as the experiment user** (default `buridan`):
   ```bash
   cd ~/buridan_bonsai1/pi
   bash scripts/setup_pi.sh
   sudo reboot
   ```
   It installs the dependencies, builds `rpi-rgb-led-matrix`, disables the
   on-board sound card (it conflicts with the LED driver), installs
   `buridan_server` into `~/buridan`, and installs the `buridan-server`
   service, which starts at boot.
3. **Check the panel order and orientation** with the test pattern:
   ```bash
   sudo systemctl stop buridan-server
   sudo ~/buridan/bin/buridan_server --test-pattern --led-show-refresh
   ```
   Each panel shows its number as white squares along its top edge, starting
   at its left edge (panel 1 red, 2 green, 3 blue, 4 yellow, 5 cyan,
   6 magenta). Fix `~/buridan/arena_config.json` until the numbers go
   1 … 6 around the ring with the squares at the top left
   (`panel_order`, `panel_flip_x`, `panel_flip_y`). Then note whether the
   numbers increase clockwise seen from above (`x_increases_clockwise`).
   Press Ctrl+C, then `sudo systemctl start buridan-server`.
4. If the image flickers or shows noise, increase `--led-slowdown-gpio`
   (2 by default; try 3 or 4). Add it to `ExecStart` in
   `/etc/systemd/system/buridan-server.service`, then run
   `sudo systemctl daemon-reload && sudo systemctl restart buridan-server`.

**Testing the Pi without Bonsai.** On any computer with Python and `pyzmq`:
```bash
python3 pi/tools/test_client.py --host <pi address> selftest
```
This runs every command, plays a short protocol, streams frames and checks the
events (16 checks).

**Frame rate versus colour bit depth.** The panels refresh faster with fewer
PWM bits (colour bit depth), but show fewer grey levels (2^bits per channel),
which matters for sine gratings. To measure the frame rate you actually get at
each bit depth:
```bash
python3 pi/tools/test_client.py --host <pi address> bitdepth_sweep --csv sweep.csv
```
A moving grating plays while the bit depth steps from 11 down to 1 (5 s each).
For each step the table shows the achieved frame rate (from the Pi's own frame
counter), the percentage of the requested rate, and the lowest 1-second rate.
The original bit depth is restored at the end. Options:
- `--rate 60`: check whether a given rate is held. The default, 240 Hz (the
  maximum), shows the fastest rate the panels allow at each depth. Every frame
  waits for a full panel refresh, so the result can't exceed the refresh rate.
- `--bits 11,8,5,3`: choose which bit depths to test (`11-1` by default).
- `--step-seconds 10`: measure longer.
- `--lsb-ns 50`: stop with an error unless the server was started with
  `--led-pwm-lsb-nanoseconds=50`.

`bitdepth_sweep_gb` runs the same sweep with a cyan grating, so only the green
and blue LEDs are used and red stays off. The CSV files record the colours and
the PWM step (`pwm_lsb_ns`) of each run.

**Both sweeps with 50 ns PWM steps.** The shortest PWM step
(`--led-pwm-lsb-nanoseconds`, library default 130 ns) can only be set when the
server starts. On the Pi, run:
```bash
bash ~/buridan_bonsai1/pi/tools/run_bitdepth_tests.sh
```
It stops the `buridan-server` service and starts `buridan_server` with
`--led-pwm-lsb-nanoseconds=50`. Then it runs `bitdepth_sweep` and
`bitdepth_sweep_gb`, and restarts the service, even after an error or Ctrl+C.
The tables, both CSV files and the server log are saved in
`~/buridan/bitdepth_results/<date>_<time>/`. If the service file has extra
flags (for example `--led-slowdown-gpio=3`), add them to the command so the
test uses the same settings. The script needs `python3-zmq` on the Pi
(`sudo apt install python3-zmq`). Short PWM steps can cause ghosting or dimmer
images on some panels, so look at the panels while the sweep runs.

To keep a bit depth permanently, add `--led-pwm-bits=N` to `ExecStart` in the
service file (see step 4 above). `set_pwm_bits N` changes it only until the
server restarts.

**Previewing a protocol without the arena.**
`make -C pi preview` builds `bin/render_preview`, which writes PPM images of a
protocol at chosen times:
`pi/bin/render_preview pi/protocols/optomotor_cw_ccw.json /tmp/opto 0 30 90`.

---

## 2. Windows PC (Bonsai)

1. Install the [.NET SDK 8](https://dotnet.microsoft.com/download). You only
   need it to rebuild the `Buridan.Bonsai` package; a built package is already
   in `bonsai/packages`.
2. Double-click **`bonsai/.bonsai/Setup.cmd`**. It downloads Bonsai 2.9.1
   into `bonsai/.bonsai` and installs the packages listed in
   `bonsai/.bonsai/Bonsai.config`: Bonsai.Vision, Bonsai.Gui, Bonsai.ZeroMQ,
   BonVision, and Buridan.Bonsai from `bonsai/packages`.
3. Double-click **`bonsai/Start.cmd`** to open `BuridanArena.bonsai`.

After changing the C# code in `bonsai/src/Buridan.Bonsai`:
1. Raise `<Version>` in its `.csproj`.
2. Run `bonsai/build.ps1`.
3. Set the same version in `.bonsai/Bonsai.config`, then run `Setup.cmd` again (or use the Bonsai package manager).

---

## 3. Running an experiment

1. Press **Start** (F5) in Bonsai.
2. Double-click the **MainWindow** node (bottom right of the workflow) to
   open the control window. Bonsai remembers it for the next time.
3. In the settings grid (left):
   - **3. Pi connection**: set the Pi host (IP address or `raspberrypi.local`).
     The username and password default to `buridan`. The status panel at the
     bottom left turns green when the Pi answers.
   - **1. Experiment**: choose the **Data folder** (the "…" button opens a
     folder browser), and set the animal ID, experimenter and notes.
   - **4. Camera**: camera index and threshold type.
4. Adjust the **threshold slider** until the animal is white and everything
   else black in the binary image. If the animal is black, switch
   *Threshold type* between `BinaryInv` and `Binary`.
5. Choose a protocol in the drop-down list and press **Start experiment**.
   Recording starts: a session folder is created, and the protocol plays on
   the ring.
6. Recording stops automatically when the protocol ends, or when you press
   **Stop experiment**. The log (bottom right) shows where the data was saved.

Camera preview and tracking run as soon as the workflow runs, so the threshold
can be tuned before starting. Video and data are only recorded between
**Start experiment** and the end of the run.

## 4. Creating a protocol

1. In the settings grid, expand **2. Protocol editor → Protocol**.
2. Set **Name** (letters, digits, `_`, `-`) and **FrameRateHz**.
3. Click "…" next to **Epochs** to add, remove or reorder epochs. For each
   epoch, choose its **Type** (Stripes, Grating or Blank) and
   **DurationSeconds**, then set the parameters in the matching category.
   Parameters of other types are ignored.
4. Press **Save protocol**. The protocol is checked, saved to
   `bonsai/protocols/<name>.json`, and uploaded to the Pi
   (`~/buridan/protocols`). It then appears in the drop-down list. The log
   shows any problem, for example a duty cycle above 1.

You can also write protocol files by hand (see
[protocol-schema.md](protocol-schema.md)) and press **Refresh protocol list**.

## 5. Buttons for the Pi

- **Show test pattern**: panel identification pattern (see section 1).
- **Blank ring**: stops whatever is shown and turns the LEDs off.
- **Restart Pi server**: restarts `buridan_server` over SSH using the
  username and password from the grid. The first time, the log shows the
  Pi's SSH host key fingerprint. Copy it into *Pi host key (optional)* so
  that later connections are refused if the key changes.

## 6. Security notes

- The default credentials (`buridan` / `buridan`) are stored in plain text in
  the workflow file. Change the Pi password, and keep the Pi on a separate
  lab network or a direct cable to the PC.
- `buridan_server` accepts commands from any computer that can reach ports
  5555–5557, and has no authentication. Do not expose it to other networks.
