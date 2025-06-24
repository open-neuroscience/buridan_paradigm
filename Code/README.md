## Install the library
1. Enable ssh
2. Make sure to update rpi
3. ssh from computer (user@)
4. `git clone https://github.com/hzeller/rpi-rgb-led-matrix/`
5. `cd rpi-rgb-led/`
6. `cd lib/`
7. `make`
8. `cd ..`
9. `make` (root rpi led folder)
## Disable sound snd_bcm2835
1. `cd /etc/modprobe.d`
2. `sudo nano alsa-blacklist.conf`
3. type/ add 'blacklist snd_bcm2835'
4. exit nano (ctrl+X,Y)
5. `sudo reboot`
(As seen [here](https://www.instructables.com/Disable-the-Built-in-Sound-Card-of-Raspberry-Pi/))

## Use the demo
1. `cd rpi-rgb-led-matrix/examples-api-use`
2. `./demo -D 1 runtext.ppm`
3. Ctrl+C to quit
4. `./demo help` to explore what else you can do
5. `sudo ./demo --led-rows=64 --led-cols=64 --led-chain=2 --led-parallel=3 --led-slowdown-gpio=4 --led-pixel-mapper="U-mapper;rotate:90" -D1 runtext.ppm` is a good example of what the closest thing to working
## Using the custom pixel mapper
In an attempt to maximise frame-rate, we wanted to use 3 parallel chains of 2 rather than 1 chain of 6. The custom pixel mapper allows the 3 parallel chains of 2 in the setup to behave as one long continuous banner. See the example at the end of this doc - since the screens are upside-down they still require a 180 degree rotation.
#### Make Files:
1. `nano lib/horizontal_mapper.cc`
2. copy-paste the contents of horizontal_mapper.cc from this repo
3. `nano lib/horizontal_mapper.h`
4. copy-paste the contents of the `horizontal_mapper.h` file in this repo
#### Update Existing Files:
1.  `nano lib/pixel-mapper.cc`
2.  At the top of the file, add the following, after the includes:
	> #include "horizontal_mapper.h" 
3.  Find the `CreateMapperMap()` function
4.  After the lines that say `RegisterPixelMapperInternal(....)`, add the following (BEFORE `return result;`):
	> RegisterPixelMapperInternal(result, new HorizontalMapper());
5.  Save and quit the file
	* For nano, this is Ctrl+X
6. `nano lib/Makefile`
7.  Near the top of the file there is a line(s) that starts: `OBJECTS = file1.o file2.o etc...`
8. Add `horizontal_mapper.o` after the last `.o` string
	* Note that there may be multiple lines, separated by a `\` - this is fine, just add to the end of the last .o line
9. Save and quit the file
#### Remake the library(?)
1. `cd lib/`
2. `make`
3. `cd ..`
4. `make`
#### Example use in examples-api-use/:
`sudo ./demo --led-rows=64 --led-cols=64 --led-chain=2 --led-parallel=3 --led-slowdown-gpio=4 --led-pixel-mapper="horizontal_mapper;rotate:180" -D1 runtext.ppm`
