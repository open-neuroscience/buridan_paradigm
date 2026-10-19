// =============================================================================
// PiCommandSender.cs
//
// Sends one JSON command to buridan_server and waits for its reply.
//
// A fresh ZeroMQ REQ socket is used for every command ("lazy pirate" pattern).
// This matters because a REQ socket that never receives a reply is stuck
// forever: with a new socket per command, an unreachable Pi only costs one
// timeout, and the next command can succeed as soon as the Pi is back.
// Commands are sent at a low rate (button clicks, 1 Hz heartbeat), so the
// cost of creating a socket each time is negligible.
// =============================================================================
using System;
using System.Diagnostics;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides a method to send JSON commands to buridan_server.
    /// </summary>
    public static class PiCommandSender
    {
        /// <summary>
        /// Sends a command to the named connection and returns the reply.
        /// Never throws: every problem is reported as a failed <see cref="PiReply"/>.
        /// </summary>
        public static PiReply Send(string connectionName, JObject request)
        {
            string command = "unknown";
            JToken commandToken = request["cmd"];
            if (commandToken != null)
            {
                command = commandToken.Value<string>();
            }

            PiConnection connection = PiConnectionRegistry.TryGet(connectionName);
            if (connection == null)
            {
                string message = "No Pi connection named '" + connectionName + "'. Add a CreatePiConnection node.";
                return PiReply.Failure(command, message);
            }

            try
            {
                return SendToAddress(connection.CommandAddress, request, command, connection.ReplyTimeout);
            }
            catch (Exception error)
            {
                return PiReply.Failure(command, error.Message);
            }
        }

        static PiReply SendToAddress(string address, JObject request, string command, TimeSpan timeout)
        {
            string requestText = request.ToString(Formatting.None);
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (RequestSocket socket = new RequestSocket())
            {
                // Do not keep unsent messages around after the socket is closed.
                socket.Options.Linger = TimeSpan.Zero;
                socket.Connect(address);

                bool sent = socket.TrySendFrame(timeout, requestText);
                if (!sent)
                {
                    return PiReply.Failure(command, "Could not send to " + address + " (timeout).");
                }

                string replyText;
                bool received = socket.TryReceiveFrameString(timeout, out replyText);
                if (!received)
                {
                    string seconds = timeout.TotalSeconds.ToString("0.0");
                    return PiReply.Failure(command, "No reply from " + address + " within " + seconds + " s.");
                }

                stopwatch.Stop();
                JObject body = JObject.Parse(replyText);

                PiReply reply = new PiReply();
                reply.Command = command;
                reply.Body = body;
                reply.RoundTripMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                reply.Timestamp = DateTimeOffset.Now;

                JToken okToken = body["ok"];
                reply.Ok = okToken != null && okToken.Type == JTokenType.Boolean && okToken.Value<bool>();

                JToken errorToken = body["error"];
                if (errorToken != null)
                {
                    reply.Error = errorToken.Value<string>();
                }
                else
                {
                    reply.Error = string.Empty;
                }
                return reply;
            }
        }

        /// <summary>
        /// Builds a request object containing only the "cmd" field.
        /// </summary>
        public static JObject CreateRequest(string command)
        {
            JObject request = new JObject();
            request["cmd"] = command;
            return request;
        }
    }
}
