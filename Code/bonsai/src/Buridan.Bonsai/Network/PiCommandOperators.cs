// =============================================================================
// PiCommandOperators.cs
//
// Small operators that each send one kind of command to buridan_server.
//
// Every operator:
//   - sends its command once per input notification,
//   - outputs a PiReply (never throws, so a missing Pi never stops the workflow),
//   - runs the network call on a background thread, so the GUI never freezes
//     while waiting for a reply.
// =============================================================================
using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Bonsai;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents an operator that sends a raw JSON command string to the Pi.
    /// </summary>
    [Description("Sends a raw JSON command (e.g. {\"cmd\":\"status\"}) to the Pi and outputs the reply.")]
    public class SendPiCommand : Transform<string, PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public SendPiCommand()
        {
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Sends each JSON string as a command.</summary>
        public override IObservable<PiReply> Process(IObservable<string> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(requestText =>
            {
                JObject request;
                try
                {
                    request = JObject.Parse(requestText);
                }
                catch (Exception error)
                {
                    return PiReply.Failure("invalid", "Not valid JSON: " + error.Message);
                }
                return PiCommandSender.Send(Connection, request);
            });
        }
    }

    /// <summary>
    /// Represents an operator that asks the Pi for its current status.
    /// </summary>
    [Description("Asks the Pi for its current status (mode, frame counter, protocol, epoch) on every input.")]
    public class GetPiStatus : Combinator<PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public GetPiStatus()
        {
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Sends a status command for every input.</summary>
        public override IObservable<PiReply> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                JObject request = PiCommandSender.CreateRequest("status");
                return PiCommandSender.Send(Connection, request);
            });
        }
    }

    /// <summary>
    /// Represents an operator that switches the Pi to a display mode.
    /// </summary>
    [Description("Switches the Pi to the selected display mode (blank, stream or test pattern) on every input.")]
    public class SetPiMode : Combinator<PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public SetPiMode()
        {
            Connection = CreatePiConnection.DefaultName;
            Mode = PiDisplayMode.TestPattern;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Gets or sets the display mode to select.</summary>
        [Description("The display mode to select.")]
        public PiDisplayMode Mode { get; set; }

        /// <summary>Sends a set_mode command for every input.</summary>
        public override IObservable<PiReply> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                JObject request = PiCommandSender.CreateRequest("set_mode");
                request["mode"] = ProtocolJson.DisplayModeToJson(Mode);
                return PiCommandSender.Send(Connection, request);
            });
        }
    }

    /// <summary>
    /// Represents an operator that stops whatever the Pi is showing.
    /// </summary>
    [Description("Stops the running protocol (or stream) and blanks the LED ring on every input.")]
    public class StopPi : Combinator<PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public StopPi()
        {
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Sends a stop command for every input.</summary>
        public override IObservable<PiReply> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                JObject request = PiCommandSender.CreateRequest("stop");
                return PiCommandSender.Send(Connection, request);
            });
        }
    }

    /// <summary>
    /// Represents an operator that plays a protocol on the Pi.
    /// </summary>
    [Description("Plays each input protocol on the Pi. The protocol is sent in full, so exactly the PC version is played.")]
    public class StartPiProtocol : Transform<ProtocolDefinition, PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public StartPiProtocol()
        {
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Sends a start_protocol command for every input protocol.</summary>
        public override IObservable<PiReply> Process(IObservable<ProtocolDefinition> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(protocol =>
            {
                return Start(Connection, protocol);
            });
        }

        /// <summary>
        /// Sends a protocol to the Pi and starts playing it immediately.
        /// </summary>
        public static PiReply Start(string connectionName, ProtocolDefinition protocol)
        {
            try
            {
                protocol.Validate();
            }
            catch (ArgumentException error)
            {
                return PiReply.Failure("start_protocol", error.Message);
            }
            JObject request = PiCommandSender.CreateRequest("start_protocol");
            request["protocol"] = ProtocolJson.ToJObject(protocol);
            return PiCommandSender.Send(connectionName, request);
        }
    }

    /// <summary>
    /// Represents an operator that stores a protocol on the Pi.
    /// </summary>
    [Description("Saves each input protocol on the Pi (in ~/buridan/protocols) without playing it.")]
    public class UploadPiProtocol : Transform<ProtocolDefinition, PiReply>
    {
        /// <summary>Creates the operator.</summary>
        public UploadPiProtocol()
        {
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Sends an upload_protocol command for every input protocol.</summary>
        public override IObservable<PiReply> Process(IObservable<ProtocolDefinition> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(protocol =>
            {
                return Upload(Connection, protocol);
            });
        }

        /// <summary>
        /// Sends a protocol to the Pi, which saves it as &lt;name&gt;.json.
        /// </summary>
        public static PiReply Upload(string connectionName, ProtocolDefinition protocol)
        {
            try
            {
                protocol.Validate();
            }
            catch (ArgumentException error)
            {
                return PiReply.Failure("upload_protocol", error.Message);
            }
            JObject request = PiCommandSender.CreateRequest("upload_protocol");
            request["protocol"] = ProtocolJson.ToJObject(protocol);
            return PiCommandSender.Send(connectionName, request);
        }
    }
}
