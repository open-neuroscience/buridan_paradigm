// =============================================================================
// MonitorPiConnection.cs
//
// Periodically asks the Pi for its status and reports whether it is reachable.
// The output (PiConnectionState) is shown in the GUI by
// PiConnectionStateVisualizer as a green / red status panel.
// =============================================================================
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using Bonsai;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Describes the reachability and state of the Pi at one moment.
    /// </summary>
    public class PiConnectionState
    {
        /// <summary>Gets or sets the connection name.</summary>
        public string ConnectionName { get; set; }

        /// <summary>Gets or sets the host that was contacted.</summary>
        public string Host { get; set; }

        /// <summary>Gets or sets whether the Pi answered the last status request.</summary>
        public bool IsConnected { get; set; }

        /// <summary>Gets or sets the round-trip time of the last request, in milliseconds.</summary>
        public double RoundTripMilliseconds { get; set; }

        /// <summary>Gets or sets the display mode reported by the Pi.</summary>
        public string Mode { get; set; }

        /// <summary>Gets or sets the name of the protocol being played, if any.</summary>
        public string Protocol { get; set; }

        /// <summary>Gets or sets the index of the current epoch (-1 if none).</summary>
        public int Epoch { get; set; }

        /// <summary>Gets or sets the type of the current epoch.</summary>
        public string EpochType { get; set; }

        /// <summary>Gets or sets the frame rate measured on the Pi.</summary>
        public double FramesPerSecond { get; set; }

        /// <summary>Gets or sets the error message when the Pi is not reachable.</summary>
        public string Error { get; set; }

        /// <summary>Gets or sets the PC time of this measurement.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Returns a multi-line human-readable description.
        /// </summary>
        public override string ToString()
        {
            StringBuilder text = new StringBuilder();
            string time = Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (IsConnected)
            {
                string roundTrip = RoundTripMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
                string fps = FramesPerSecond.ToString("0.0", CultureInfo.InvariantCulture);
                text.AppendLine("Pi CONNECTED  " + Host + "  (" + roundTrip + " ms, " + time + ")");
                text.Append("Mode: " + Mode + "   Panel fps: " + fps);
                if (!string.IsNullOrEmpty(Protocol))
                {
                    text.AppendLine();
                    text.Append("Protocol: " + Protocol + "   Epoch " + Epoch + " (" + EpochType + ")");
                }
            }
            else
            {
                text.AppendLine("Pi NOT CONNECTED  " + Host + "  (" + time + ")");
                text.Append(Error);
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Represents an operator that periodically checks whether the Pi is
    /// reachable and reports its current state.
    /// </summary>
    [Description("Periodically checks whether the Pi is reachable and reports its state (mode, protocol, epoch, frame rate).")]
    public class MonitorPiConnection : Source<PiConnectionState>
    {
        /// <summary>Creates the operator.</summary>
        public MonitorPiConnection()
        {
            Connection = CreatePiConnection.DefaultName;
            Interval = TimeSpan.FromSeconds(1.0);
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Gets or sets the time between checks.</summary>
        [Description("Time between two status checks.")]
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Checks the Pi immediately and then once per interval.
        /// </summary>
        public override IObservable<PiConnectionState> Generate()
        {
            // Recursive scheduling: the next check is only scheduled after the
            // current one has finished, so slow replies (e.g. a 2 s timeout
            // while the Pi is off) never pile up.
            return Observable.Create<PiConnectionState>(observer =>
            {
                return Scheduler.Default.Schedule(TimeSpan.Zero, reschedule =>
                {
                    PiConnectionState state = Check();
                    observer.OnNext(state);
                    reschedule(Interval);
                });
            });
        }

        PiConnectionState Check()
        {
            PiConnectionState state = new PiConnectionState();
            state.ConnectionName = Connection;
            state.Epoch = -1;
            state.Mode = string.Empty;
            state.Protocol = string.Empty;
            state.EpochType = string.Empty;
            state.Error = string.Empty;

            PiConnection connection = PiConnectionRegistry.TryGet(Connection);
            if (connection != null)
            {
                state.Host = connection.Host;
            }
            else
            {
                state.Host = "(no connection named '" + Connection + "')";
            }

            JObject request = PiCommandSender.CreateRequest("status");
            PiReply reply = PiCommandSender.Send(Connection, request);
            state.Timestamp = reply.Timestamp;
            state.IsConnected = reply.Ok;
            state.RoundTripMilliseconds = reply.RoundTripMilliseconds;

            if (!reply.Ok)
            {
                state.Error = reply.Error;
                return state;
            }

            JObject body = reply.Body;
            state.Mode = ReadString(body, "mode");
            state.Protocol = ReadString(body, "protocol");
            state.EpochType = ReadString(body, "epoch_type");
            JToken epochToken = body["epoch"];
            if (epochToken != null)
            {
                state.Epoch = epochToken.Value<int>();
            }
            JToken fpsToken = body["fps"];
            if (fpsToken != null)
            {
                state.FramesPerSecond = fpsToken.Value<double>();
            }
            return state;
        }

        static string ReadString(JObject body, string key)
        {
            JToken token = body[key];
            if (token == null)
            {
                return string.Empty;
            }
            return token.Value<string>();
        }
    }
}
