// =============================================================================
// CreatePiConnection.cs
//
// Declares the network settings of the Raspberry Pi that drives the LED ring.
//
// This follows the pattern of Bonsai's CreateSerialPort: one CreatePiConnection
// node holds the settings under a name (default "Arena"), and every other Pi
// operator refers to it through its Connection property.
//
// The settings are read at the moment each command is sent, so changing the
// Host in the property grid while the workflow runs takes effect immediately.
// =============================================================================
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Bonsai;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents an operator that registers the network settings of the
    /// Buridan arena Raspberry Pi for use by the other Pi operators.
    /// </summary>
    [Description("Registers the network settings of the Buridan arena Raspberry Pi for use by the other Pi operators.")]
    public class CreatePiConnection : Source<PiConnection>
    {
        /// <summary>
        /// The connection name used by default by every Pi operator.
        /// </summary>
        public const string DefaultName = "Arena";

        /// <summary>
        /// Creates the operator with the default settings.
        /// </summary>
        public CreatePiConnection()
        {
            Name = DefaultName;
            Host = "raspberrypi.local";
            Username = "buridan";
            Password = "buridan";
            CommandPort = 5555;
            FramePort = 5556;
            StatusPort = 5557;
            ReplyTimeoutMilliseconds = 2000;
            HostKeyFingerprint = string.Empty;
        }

        /// <summary>
        /// Gets or sets the name other operators use to refer to this connection.
        /// </summary>
        [Category("Connection")]
        [Description("The name other Pi operators use to refer to this connection.")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the host name or IP address of the Pi.
        /// </summary>
        [Category("Connection")]
        [Description("Host name or IP address of the Raspberry Pi, e.g. 192.168.1.50 or raspberrypi.local.")]
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the SSH user name on the Pi.
        /// </summary>
        [Category("Login (SSH)")]
        [Description("User name for SSH login on the Pi (used to restart buridan_server).")]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the SSH password on the Pi.
        /// </summary>
        [Category("Login (SSH)")]
        [PasswordPropertyText(true)]
        [Description("Password for SSH login on the Pi (used to restart buridan_server).")]
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the expected SSH host key fingerprint (optional).
        /// </summary>
        [Category("Login (SSH)")]
        [Description("Optional. SHA256 fingerprint of the Pi's SSH host key (as printed by 'ssh-keygen -lf'). " +
                     "When set, SSH refuses to connect to a machine with a different key.")]
        public string HostKeyFingerprint { get; set; }

        /// <summary>
        /// Gets or sets the TCP port of the command socket.
        /// </summary>
        [Category("Ports")]
        [Description("TCP port of the buridan_server command socket (REQ/REP).")]
        public int CommandPort { get; set; }

        /// <summary>
        /// Gets or sets the TCP port of the streamed-frame socket.
        /// </summary>
        [Category("Ports")]
        [Description("TCP port of the buridan_server streamed-frame socket (PUSH/PULL).")]
        public int FramePort { get; set; }

        /// <summary>
        /// Gets or sets the TCP port of the status socket.
        /// </summary>
        [Category("Ports")]
        [Description("TCP port of the buridan_server status/event socket (PUB/SUB).")]
        public int StatusPort { get; set; }

        /// <summary>
        /// Gets or sets how long to wait for a command reply.
        /// </summary>
        [Category("Connection")]
        [Description("How long to wait for a reply to a command before reporting the Pi as unreachable, in milliseconds.")]
        public int ReplyTimeoutMilliseconds { get; set; }

        /// <summary>
        /// Registers the connection and emits it once. The registration is
        /// removed when the workflow stops.
        /// </summary>
        public override IObservable<PiConnection> Generate()
        {
            return Observable.Create<PiConnection>(observer =>
            {
                PiConnection connection = new PiConnection(this);
                IDisposable registration = PiConnectionRegistry.Register(Name, connection);
                observer.OnNext(connection);
                return Disposable.Create(() =>
                {
                    registration.Dispose();
                });
            });
        }
    }
}
