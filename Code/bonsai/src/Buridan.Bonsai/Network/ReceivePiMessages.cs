// =============================================================================
// ReceivePiMessages.cs
//
// Receives the "status" and "event" messages published by buridan_server
// (ZeroMQ SUB socket, default port 5557).
//
// Each message has two parts: a topic ("status" or "event") and a JSON
// payload. Events include epoch_start, protocol_end, protocol_stopped,
// mode_changed and protocol_saved; each carries the Pi frame number and the
// Pi clocks (t_mono, t_wall). The PC receive time is added here, so the Pi
// events can be aligned with camera frames recorded on the PC.
// =============================================================================
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bonsai;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents one status or event message received from the Pi.
    /// </summary>
    public class PiMessage
    {
        /// <summary>Gets or sets the topic: "status" or "event".</summary>
        public string Topic { get; set; }

        /// <summary>Gets or sets the JSON payload.</summary>
        public JObject Payload { get; set; }

        /// <summary>Gets or sets the PC time at which the message was received.</summary>
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>Gets whether this message is an event (rather than a periodic status).</summary>
        public bool IsEvent
        {
            get { return Topic == "event"; }
        }

        /// <summary>Gets the event name (e.g. "epoch_start"), or an empty string for status messages.</summary>
        public string EventName
        {
            get
            {
                JToken token = Payload["event"];
                if (token == null)
                {
                    return string.Empty;
                }
                return token.Value<string>();
            }
        }

        /// <summary>Gets the payload as compact JSON text.</summary>
        public string PayloadText
        {
            get { return Payload.ToString(Formatting.None); }
        }

        /// <summary>Returns a one-line description of the message.</summary>
        public override string ToString()
        {
            string time = ReceivedAt.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return "[" + time + "] " + Topic + " " + PayloadText;
        }
    }

    /// <summary>
    /// Represents an operator that receives status and event messages
    /// published by the Pi.
    /// </summary>
    [Description("Receives the status and event messages published by buridan_server on the Pi.")]
    public class ReceivePiMessages : Source<PiMessage>
    {
        static readonly TimeSpan PollTimeout = TimeSpan.FromMilliseconds(200);

        /// <summary>Creates the operator.</summary>
        public ReceivePiMessages()
        {
            Connection = CreatePiConnection.DefaultName;
            IncludeStatus = true;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Gets or sets whether periodic status messages are emitted (events are always emitted).</summary>
        [Description("If false, only event messages are emitted and the periodic status messages are dropped.")]
        public bool IncludeStatus { get; set; }

        /// <summary>
        /// Starts a background loop that receives messages until the workflow stops.
        /// </summary>
        public override IObservable<PiMessage> Generate()
        {
            return Observable.Create<PiMessage>((observer, cancellationToken) =>
            {
                return Task.Factory.StartNew(() =>
                {
                    ReceiveLoop(observer, cancellationToken);
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            });
        }

        void ReceiveLoop(IObserver<PiMessage> observer, CancellationToken cancellationToken)
        {
            SubscriberSocket socket = null;
            string connectedAddress = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // (Re)connect whenever the connection appears or its host changes.
                    PiConnection connection = PiConnectionRegistry.TryGet(Connection);
                    string address = null;
                    if (connection != null)
                    {
                        address = connection.StatusAddress;
                    }
                    if (address != connectedAddress)
                    {
                        if (socket != null)
                        {
                            socket.Dispose();
                            socket = null;
                        }
                        connectedAddress = address;
                        if (address != null)
                        {
                            socket = new SubscriberSocket();
                            socket.Options.Linger = TimeSpan.Zero;
                            socket.Connect(address);
                            socket.SubscribeToAnyTopic();
                        }
                    }

                    if (socket == null)
                    {
                        // No connection registered yet: wait and look again.
                        cancellationToken.WaitHandle.WaitOne(PollTimeout);
                        continue;
                    }

                    // Part 1: topic. A timeout lets us re-check cancellation and host.
                    string topic;
                    bool more;
                    bool received = socket.TryReceiveFrameString(PollTimeout, out topic, out more);
                    if (!received)
                    {
                        continue;
                    }
                    if (!more)
                    {
                        // Not in the expected two-part format: ignore.
                        continue;
                    }

                    // Part 2: JSON payload (arrives together with part 1).
                    string payloadText = socket.ReceiveFrameString();

                    bool isStatus = topic == "status";
                    if (isStatus && !IncludeStatus)
                    {
                        continue;
                    }

                    PiMessage message = new PiMessage();
                    message.Topic = topic;
                    message.ReceivedAt = DateTimeOffset.Now;
                    try
                    {
                        message.Payload = JObject.Parse(payloadText);
                    }
                    catch (JsonException)
                    {
                        message.Payload = new JObject();
                        message.Payload["unparsed"] = payloadText;
                    }
                    observer.OnNext(message);
                }
                observer.OnCompleted();
            }
            catch (Exception error)
            {
                observer.OnError(error);
            }
            finally
            {
                if (socket != null)
                {
                    socket.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Represents an operator that only lets through Pi events with a given name.
    /// </summary>
    [Description("Only lets through Pi event messages with the given name (e.g. protocol_end).")]
    public class FilterPiEvent : Transform<PiMessage, PiMessage>
    {
        /// <summary>Creates the operator.</summary>
        public FilterPiEvent()
        {
            EventName = "protocol_end";
        }

        /// <summary>Gets or sets the event name to let through.</summary>
        [Description("Event name to let through, e.g. protocol_end, epoch_start, protocol_stopped.")]
        public string EventName { get; set; }

        /// <summary>Filters the message sequence.</summary>
        public override IObservable<PiMessage> Process(IObservable<PiMessage> source)
        {
            return source.Where(message =>
            {
                if (!message.IsEvent)
                {
                    return false;
                }
                return message.EventName == EventName;
            });
        }
    }
}
