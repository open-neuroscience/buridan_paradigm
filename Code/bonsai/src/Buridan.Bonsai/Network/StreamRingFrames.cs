// =============================================================================
// StreamRingFrames.cs
//
// Sends images rendered on the PC (e.g. by BonVision) to the LED ring.
//
// Each input image must be exactly 384 x 64 pixels (the unrolled ring: six
// 64 x 64 panels side by side), 8 bits per channel, either 3-channel BGR
// (OpenCV / Bonsai default) or 1-channel grayscale. It is converted to packed
// RGB (73728 bytes) and pushed to buridan_server's frame socket (port 5556).
//
// Frames are only displayed while the Pi is in "stream" mode; by default this
// operator switches the Pi to stream mode when the workflow starts.
// If the network or the Pi cannot keep up, frames are dropped rather than
// queued, so the ring always shows the most recent frame (lowest latency).
// =============================================================================
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Bonsai;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using OpenCV.Net;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents an operator that streams 384 x 64 images to the LED ring.
    /// </summary>
    [Description("Streams 384 x 64 images (BGR or grayscale, 8-bit) to the LED ring of the Buridan arena.")]
    public class StreamRingFrames : Sink<IplImage>
    {
        /// <summary>Width of the unrolled LED ring, in pixels.</summary>
        public const int RingWidth = 384;

        /// <summary>Height of the LED ring, in pixels.</summary>
        public const int RingHeight = 64;

        /// <summary>Size of one packed RGB ring frame, in bytes.</summary>
        public const int RingFrameBytes = RingWidth * RingHeight * 3;

        /// <summary>Creates the operator.</summary>
        public StreamRingFrames()
        {
            Connection = CreatePiConnection.DefaultName;
            SetStreamModeOnStart = true;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Gets or sets whether the Pi is switched to stream mode when the workflow starts.</summary>
        [Description("If true, the Pi is switched to stream mode when the workflow starts.")]
        public bool SetStreamModeOnStart { get; set; }

        /// <summary>
        /// Sends every input image to the Pi and passes it through unchanged.
        /// </summary>
        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return Observable.Using(
                () =>
                {
                    if (SetStreamModeOnStart)
                    {
                        // Sent in the background so the workflow start is never delayed.
                        string connectionName = Connection;
                        Task.Run(() =>
                        {
                            JObject request = PiCommandSender.CreateRequest("set_mode");
                            request["mode"] = "stream";
                            PiCommandSender.Send(connectionName, request);
                        });
                    }
                    return new FrameStreamer(Connection);
                },
                streamer => source.Do(image => streamer.Send(image)));
        }

        /// <summary>
        /// Converts an image into packed RGB bytes in ring layout.
        /// Throws <see cref="ArgumentException"/> if the image has the wrong format.
        /// </summary>
        public static byte[] ToPackedRgb(IplImage image)
        {
            if (image.Width != RingWidth || image.Height != RingHeight)
            {
                throw new ArgumentException("Ring frames must be 384 x 64 pixels, got " +
                                            image.Width + " x " + image.Height + ".");
            }
            if (image.Depth != IplDepth.U8)
            {
                throw new ArgumentException("Ring frames must have 8 bits per channel.");
            }
            int channels = image.Channels;
            if (channels != 1 && channels != 3)
            {
                throw new ArgumentException("Ring frames must have 1 (gray) or 3 (BGR) channels.");
            }

            byte[] packed = new byte[RingFrameBytes];
            int rowBytes = RingWidth * channels;
            byte[] row = new byte[rowBytes];

            for (int y = 0; y < RingHeight; y++)
            {
                // Rows may be padded, so each row is located using WidthStep.
                IntPtr rowStart = IntPtr.Add(image.ImageData, y * image.WidthStep);
                Marshal.Copy(rowStart, row, 0, rowBytes);

                for (int x = 0; x < RingWidth; x++)
                {
                    int target = (y * RingWidth + x) * 3;
                    if (channels == 3)
                    {
                        // OpenCV stores pixels as blue, green, red.
                        int sourceIndex = x * 3;
                        packed[target + 0] = row[sourceIndex + 2];
                        packed[target + 1] = row[sourceIndex + 1];
                        packed[target + 2] = row[sourceIndex + 0];
                    }
                    else
                    {
                        byte gray = row[x];
                        packed[target + 0] = gray;
                        packed[target + 1] = gray;
                        packed[target + 2] = gray;
                    }
                }
            }
            return packed;
        }

        /// <summary>
        /// Owns the PUSH socket and reconnects it if the Pi host changes.
        /// </summary>
        sealed class FrameStreamer : IDisposable
        {
            // Keep at most two frames waiting: we only care about the newest.
            const int SendHighWaterMark = 2;

            readonly string connectionName;
            PushSocket socket;
            string connectedAddress;

            public FrameStreamer(string connectionName)
            {
                this.connectionName = connectionName;
            }

            public void Send(IplImage image)
            {
                byte[] packed = ToPackedRgb(image);

                PiConnection connection = PiConnectionRegistry.TryGet(connectionName);
                if (connection == null)
                {
                    // No connection declared (yet): drop the frame.
                    return;
                }

                string address = connection.FrameAddress;
                if (address != connectedAddress)
                {
                    if (socket != null)
                    {
                        socket.Dispose();
                    }
                    socket = new PushSocket();
                    socket.Options.Linger = TimeSpan.Zero;
                    socket.Options.SendHighWatermark = SendHighWaterMark;
                    socket.Connect(address);
                    connectedAddress = address;
                }

                // Zero timeout: if the frame cannot be queued right now, drop it.
                socket.TrySendFrame(TimeSpan.Zero, packed);
            }

            public void Dispose()
            {
                if (socket != null)
                {
                    socket.Dispose();
                    socket = null;
                }
            }
        }
    }
}
