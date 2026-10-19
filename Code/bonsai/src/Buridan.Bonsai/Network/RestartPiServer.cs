// =============================================================================
// RestartPiServer.cs
//
// Restarts buridan_server on the Pi over SSH, using the user name and password
// of the CreatePiConnection node.
//
// buridan_server runs as a systemd service (installed by pi/scripts/setup_pi.sh),
// so the command executed on the Pi is:
//     sudo systemctl restart buridan-server
// The password is written to sudo's standard input (sudo -S), never onto the
// command line.
//
// Host key: if CreatePiConnection.HostKeyFingerprint is set, the connection is
// refused when the Pi's key does not match. Otherwise any key is accepted and
// its fingerprint is reported, so it can be copied into HostKeyFingerprint.
// =============================================================================
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bonsai;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents an operator that restarts buridan_server on the Pi over SSH.
    /// </summary>
    [Description("Restarts buridan_server on the Pi over SSH (sudo systemctl restart buridan-server) on every input.")]
    public class RestartPiServer : Combinator<string>
    {
        // Characters allowed in a systemd unit name.
        static readonly Regex ValidServiceNamePattern = new Regex("^[A-Za-z0-9@._-]+$");

        /// <summary>Creates the operator.</summary>
        public RestartPiServer()
        {
            Connection = CreatePiConnection.DefaultName;
            ServiceName = "buridan-server";
            TimeoutSeconds = 15;
        }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use (host, user name and password).")]
        public string Connection { get; set; }

        /// <summary>Gets or sets the systemd service name of buridan_server.</summary>
        [Description("Name of the systemd service running buridan_server.")]
        public string ServiceName { get; set; }

        /// <summary>Gets or sets the SSH timeout in seconds.</summary>
        [Description("Maximum time to connect and run the restart command, in seconds.")]
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// Restarts the server once per input and outputs a one-line report.
        /// </summary>
        public override IObservable<string> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                try
                {
                    string result = Restart();
                    return "[" + time + "] " + result;
                }
                catch (Exception error)
                {
                    return "[" + time + "] Restart FAILED: " + error.Message;
                }
            });
        }

        string Restart()
        {
            PiConnection connection = PiConnectionRegistry.TryGet(Connection);
            if (connection == null)
            {
                throw new InvalidOperationException("No Pi connection named '" + Connection + "'.");
            }

            string expectedFingerprint = connection.HostKeyFingerprint;
            string receivedFingerprint = string.Empty;

            using (SshClient client = new SshClient(connection.Host, connection.Username, connection.Password))
            {
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
                client.HostKeyReceived += (sender, e) =>
                {
                    receivedFingerprint = "SHA256:" + e.FingerPrintSHA256;
                    if (string.IsNullOrEmpty(expectedFingerprint))
                    {
                        e.CanTrust = true;
                    }
                    else
                    {
                        e.CanTrust = FingerprintsMatch(expectedFingerprint, receivedFingerprint);
                    }
                };

                try
                {
                    client.Connect();
                }
                catch (SshConnectionException error)
                {
                    throw new InvalidOperationException(
                        "SSH connection failed (host key " + receivedFingerprint + "): " + error.Message, error);
                }

                // The service name becomes part of a shell command, so only
                // allow characters that systemd unit names use.
                if (!ValidServiceNamePattern.IsMatch(ServiceName))
                {
                    throw new InvalidOperationException("Invalid service name '" + ServiceName + "'.");
                }
                string commandText = "sudo -S -p '' systemctl restart " + ServiceName;

                using (SshCommand command = client.CreateCommand(commandText))
                {
                    command.CommandTimeout = TimeSpan.FromSeconds(TimeoutSeconds);
                    IAsyncResult execution = command.BeginExecute();

                    // "sudo -S" reads the password from standard input. Writing it
                    // there (instead of on the command line) keeps it out of the
                    // Pi's process list. Disposing the stream sends end-of-input.
                    using (Stream input = command.CreateInputStream())
                    {
                        byte[] passwordLine = Encoding.UTF8.GetBytes(connection.Password + "\n");
                        input.Write(passwordLine, 0, passwordLine.Length);
                    }
                    command.EndExecute(execution);
                    client.Disconnect();

                    int exitStatus = -1;
                    if (command.ExitStatus.HasValue)
                    {
                        exitStatus = command.ExitStatus.Value;
                    }
                    if (exitStatus != 0)
                    {
                        throw new InvalidOperationException("restart command exited with " + exitStatus + ": " + command.Error.Trim());
                    }
                }

                string report = "buridan_server restarted on " + connection.Host;
                if (string.IsNullOrEmpty(expectedFingerprint))
                {
                    report += " (host key " + receivedFingerprint + " not verified; copy it into HostKeyFingerprint)";
                }
                return report;
            }
        }

        static bool FingerprintsMatch(string expected, string received)
        {
            // Accept the fingerprint with or without the "SHA256:" prefix and
            // with or without base64 padding.
            string normalizedExpected = expected.Trim();
            if (normalizedExpected.StartsWith("SHA256:", StringComparison.Ordinal))
            {
                normalizedExpected = normalizedExpected.Substring("SHA256:".Length);
            }
            normalizedExpected = normalizedExpected.TrimEnd('=');

            string normalizedReceived = received;
            if (normalizedReceived.StartsWith("SHA256:", StringComparison.Ordinal))
            {
                normalizedReceived = normalizedReceived.Substring("SHA256:".Length);
            }
            normalizedReceived = normalizedReceived.TrimEnd('=');

            return string.Equals(normalizedExpected, normalizedReceived, StringComparison.Ordinal);
        }
    }
}
