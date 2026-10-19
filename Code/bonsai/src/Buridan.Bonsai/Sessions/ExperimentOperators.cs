// =============================================================================
// ExperimentOperators.cs
//
// StartExperiment: creates a session folder, starts recording and plays the
//                  selected protocol on the Pi.
// StopExperiment:  stops the protocol on the Pi and closes the session.
//
// Both output an ExperimentStatus (a line of text for the GUI log) and never
// throw, so a problem (e.g. Pi unreachable) is reported without stopping the
// workflow.
// =============================================================================
using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Bonsai;
using Bonsai.Design;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Describes the outcome of starting or stopping an experiment.
    /// </summary>
    public class ExperimentStatus
    {
        /// <summary>Gets or sets whether the action succeeded.</summary>
        public bool Ok { get; set; }

        /// <summary>Gets or sets a human-readable description of what happened.</summary>
        public string Message { get; set; }

        /// <summary>Gets or sets the session concerned, if any.</summary>
        public ExperimentSession Session { get; set; }

        /// <summary>Gets or sets the PC time of the action.</summary>
        public DateTimeOffset Timestamp { get; set; }

        internal static ExperimentStatus Create(bool ok, string message, ExperimentSession session)
        {
            ExperimentStatus status = new ExperimentStatus();
            status.Ok = ok;
            status.Message = message;
            status.Session = session;
            status.Timestamp = DateTimeOffset.Now;
            return status;
        }

        /// <summary>Returns the message prefixed with the time.</summary>
        public override string ToString()
        {
            string time = Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            return "[" + time + "] " + Message;
        }
    }

    /// <summary>
    /// Represents an operator that starts an experiment for each input protocol name.
    /// </summary>
    [Description("For each input protocol name: creates a session folder, starts recording and plays the protocol on the Pi.")]
    public class StartExperiment : Transform<string, ExperimentStatus>
    {
        /// <summary>Creates the operator.</summary>
        public StartExperiment()
        {
            DataFolder = "data";
            AnimalId = "fly01";
            Experimenter = string.Empty;
            Notes = string.Empty;
            ProtocolFolder = "protocols";
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the folder where session folders are created.</summary>
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        [Description("Folder on the PC where experiment data is saved. A new sub-folder is created for every run.")]
        public string DataFolder { get; set; }

        /// <summary>Gets or sets the identifier of the animal.</summary>
        [Description("Identifier of the animal, used in the session folder name.")]
        public string AnimalId { get; set; }

        /// <summary>Gets or sets the name of the experimenter.</summary>
        [Description("Name of the experimenter (saved in session.json).")]
        public string Experimenter { get; set; }

        /// <summary>Gets or sets free-text notes about the run.</summary>
        [Description("Free-text notes about this run (saved in session.json).")]
        public string Notes { get; set; }

        /// <summary>Gets or sets the folder containing the protocol files.</summary>
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        [Description("Folder on the PC containing the protocol files. Relative paths start at the workflow folder.")]
        public string ProtocolFolder { get; set; }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Starts one experiment per input protocol name.</summary>
        public override IObservable<ExperimentStatus> Process(IObservable<string> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(protocolName =>
            {
                return Start(protocolName);
            });
        }

        ExperimentStatus Start(string protocolName)
        {
            ExperimentSession running = SessionManager.Current;
            if (running != null)
            {
                return ExperimentStatus.Create(false, "An experiment is already running. Press Stop first.", running);
            }
            if (string.IsNullOrEmpty(protocolName))
            {
                return ExperimentStatus.Create(false, "No protocol selected.", null);
            }

            // 1) Load and check the protocol.
            ProtocolDefinition protocol;
            try
            {
                protocol = ProtocolFiles.Load(ProtocolFolder, protocolName);
                protocol.Validate();
            }
            catch (Exception error)
            {
                return ExperimentStatus.Create(false, "Cannot load protocol '" + protocolName + "': " + error.Message, null);
            }

            // 2) Create the session folder and start recording.
            string piHost = string.Empty;
            PiConnection connection = PiConnectionRegistry.TryGet(Connection);
            if (connection != null)
            {
                piHost = connection.Host;
            }
            ExperimentSession session;
            try
            {
                session = new ExperimentSession(DataFolder, AnimalId, Experimenter, Notes, protocol, piHost);
                session.CreateFolder();
                SessionManager.Start(session);
            }
            catch (Exception error)
            {
                return ExperimentStatus.Create(false, "Cannot create the session folder: " + error.Message, null);
            }

            // 3) Play the protocol on the Pi. If that fails, close the session again.
            PiReply reply = StartPiProtocol.Start(Connection, protocol);
            if (!reply.Ok)
            {
                SessionManager.Stop("pi_start_failed: " + reply.Error);
                return ExperimentStatus.Create(false, "Pi did not start the protocol: " + reply.Error, session);
            }

            string duration = protocol.TotalDurationSeconds.ToString("0", CultureInfo.InvariantCulture);
            string message = "RECORDING '" + protocol.Name + "' (" + duration + " s) to " + session.Folder;
            return ExperimentStatus.Create(true, message, session);
        }
    }

    /// <summary>
    /// Represents an operator that stops the running experiment on every input.
    /// </summary>
    [Description("Stops the running experiment: blanks the LED ring and closes the session files.")]
    public class StopExperiment : Combinator<ExperimentStatus>
    {
        /// <summary>Creates the operator.</summary>
        public StopExperiment()
        {
            SendStopToPi = true;
            Reason = "stop button";
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets whether a stop command is sent to the Pi.</summary>
        [Description("If true, the Pi is told to stop the protocol and blank the ring.")]
        public bool SendStopToPi { get; set; }

        /// <summary>Gets or sets the reason written to session.json.</summary>
        [Description("Reason for stopping, written to session.json (e.g. 'stop button' or 'protocol_end').")]
        public string Reason { get; set; }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Stops the experiment once per input.</summary>
        public override IObservable<ExperimentStatus> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                return Stop();
            });
        }

        ExperimentStatus Stop()
        {
            string piMessage = string.Empty;
            if (SendStopToPi)
            {
                PiReply reply = PiCommandSender.Send(Connection, PiCommandSender.CreateRequest("stop"));
                if (!reply.Ok)
                {
                    piMessage = " (Pi stop failed: " + reply.Error + ")";
                }
            }

            ExperimentSession stopped = SessionManager.Stop(Reason);
            if (stopped == null)
            {
                return ExperimentStatus.Create(true, "No experiment was running." + piMessage, null);
            }
            string duration = stopped.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            string message = "STOPPED (" + Reason + ") after " + duration + " s. Data saved in " + stopped.Folder + piMessage;
            return ExperimentStatus.Create(true, message, stopped);
        }
    }
}
