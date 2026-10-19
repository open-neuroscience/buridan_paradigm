// =============================================================================
// PiConnection.cs
//
// A live view of the settings held by a CreatePiConnection node, plus the
// registry that lets other operators find a connection by name.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides the current network settings of a registered Pi connection.
    /// Every property reads the value from the <see cref="CreatePiConnection"/>
    /// node at the moment it is accessed.
    /// </summary>
    public class PiConnection
    {
        readonly CreatePiConnection settings;

        internal PiConnection(CreatePiConnection settings)
        {
            this.settings = settings;
        }

        /// <summary>Gets the connection name.</summary>
        public string Name { get { return settings.Name; } }

        /// <summary>Gets the host name or IP address of the Pi.</summary>
        public string Host { get { return settings.Host; } }

        /// <summary>Gets the SSH user name.</summary>
        public string Username { get { return settings.Username; } }

        /// <summary>Gets the SSH password.</summary>
        public string Password { get { return settings.Password; } }

        /// <summary>Gets the expected SSH host key fingerprint, or an empty string.</summary>
        public string HostKeyFingerprint { get { return settings.HostKeyFingerprint; } }

        /// <summary>Gets the command socket port.</summary>
        public int CommandPort { get { return settings.CommandPort; } }

        /// <summary>Gets the streamed-frame socket port.</summary>
        public int FramePort { get { return settings.FramePort; } }

        /// <summary>Gets the status socket port.</summary>
        public int StatusPort { get { return settings.StatusPort; } }

        /// <summary>Gets the command reply timeout.</summary>
        public TimeSpan ReplyTimeout
        {
            get { return TimeSpan.FromMilliseconds(settings.ReplyTimeoutMilliseconds); }
        }

        /// <summary>Gets the ZeroMQ address of the command socket.</summary>
        public string CommandAddress { get { return "tcp://" + Host + ":" + CommandPort; } }

        /// <summary>Gets the ZeroMQ address of the streamed-frame socket.</summary>
        public string FrameAddress { get { return "tcp://" + Host + ":" + FramePort; } }

        /// <summary>Gets the ZeroMQ address of the status socket.</summary>
        public string StatusAddress { get { return "tcp://" + Host + ":" + StatusPort; } }

        /// <summary>
        /// Returns a short description of the connection (the password is never shown).
        /// </summary>
        public override string ToString()
        {
            return Name + " -> " + Host + " (ports " + CommandPort + "/" + FramePort + "/" + StatusPort + ")";
        }
    }

    /// <summary>
    /// Keeps track of the Pi connections declared by <see cref="CreatePiConnection"/> nodes.
    /// </summary>
    public static class PiConnectionRegistry
    {
        static readonly object SyncRoot = new object();
        static readonly Dictionary<string, PiConnection> Connections = new Dictionary<string, PiConnection>();

        /// <summary>
        /// Registers a connection under a name. Disposing the returned object
        /// removes the registration.
        /// </summary>
        public static IDisposable Register(string name, PiConnection connection)
        {
            lock (SyncRoot)
            {
                if (Connections.ContainsKey(name))
                {
                    throw new InvalidOperationException("A Pi connection named '" + name + "' already exists.");
                }
                Connections.Add(name, connection);
            }
            return Disposable.Create(() =>
            {
                lock (SyncRoot)
                {
                    Connections.Remove(name);
                }
            });
        }

        /// <summary>
        /// Returns the connection with the given name, or null if no
        /// <see cref="CreatePiConnection"/> node with that name is running.
        /// </summary>
        public static PiConnection TryGet(string name)
        {
            lock (SyncRoot)
            {
                PiConnection connection;
                bool found = Connections.TryGetValue(name, out connection);
                if (!found)
                {
                    return null;
                }
                return connection;
            }
        }
    }
}
