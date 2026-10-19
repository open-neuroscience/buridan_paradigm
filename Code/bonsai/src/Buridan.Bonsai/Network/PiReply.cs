// =============================================================================
// PiReply.cs
//
// The outcome of one command sent to buridan_server.
// =============================================================================
using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents the reply (or failure) of a command sent to the Pi.
    /// </summary>
    public class PiReply
    {
        /// <summary>
        /// Gets or sets the command name, e.g. "ping" or "start_protocol".
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets whether the Pi executed the command successfully.
        /// </summary>
        public bool Ok { get; set; }

        /// <summary>
        /// Gets or sets the error message when <see cref="Ok"/> is false.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets the full JSON reply, or null if no reply was received.
        /// </summary>
        public JObject Body { get; set; }

        /// <summary>
        /// Gets or sets the time between sending the command and receiving the reply.
        /// </summary>
        public double RoundTripMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the PC time at which the reply was received.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Creates a failed reply for a command that could not be sent or answered.
        /// </summary>
        public static PiReply Failure(string command, string error)
        {
            PiReply reply = new PiReply();
            reply.Command = command;
            reply.Ok = false;
            reply.Error = error;
            reply.Body = null;
            reply.RoundTripMilliseconds = double.NaN;
            reply.Timestamp = DateTimeOffset.Now;
            return reply;
        }

        /// <summary>
        /// Returns the reply body as compact JSON text, or an empty string.
        /// </summary>
        public string BodyText
        {
            get
            {
                if (Body == null)
                {
                    return string.Empty;
                }
                return Body.ToString(Formatting.None);
            }
        }

        /// <summary>
        /// Returns a one-line description, e.g. "[10:42:01] OK ping (2.1 ms)".
        /// </summary>
        public override string ToString()
        {
            string time = Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (Ok)
            {
                string roundTrip = RoundTripMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);
                return "[" + time + "] OK " + Command + " (" + roundTrip + " ms)";
            }
            return "[" + time + "] ERROR " + Command + ": " + Error;
        }
    }
}
