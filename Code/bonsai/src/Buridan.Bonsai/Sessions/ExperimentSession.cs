// =============================================================================
// ExperimentSession.cs
//
// One experiment run: a folder on the PC containing everything recorded
// between Start and Stop.
//
// Folder layout:  <DataFolder>/<yyyy-MM-dd_HH-mm-ss>_<AnimalId>_<Protocol>/
//   session.json        metadata (animal, experimenter, notes, times, Pi host)
//   protocol.json       exact copy of the protocol that was played
//   video.avi           raw camera video
//   camera_frames.csv   one line per video frame (index, times)
//   tracking.csv        animal position per frame
//   pi_messages.csv     every status / event message received from the Pi
//
// All CSV files share the column t_session_s: seconds since the session
// started, measured with the same PC stopwatch, to align video, tracking and
// stimulus events.
// =============================================================================
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents one experiment run and the files it records into.
    /// </summary>
    public class ExperimentSession
    {
        readonly Stopwatch stopwatch;

        internal ExperimentSession(
            string dataFolder,
            string animalId,
            string experimenter,
            string notes,
            ProtocolDefinition protocol,
            string piHost)
        {
            StartTime = DateTimeOffset.Now;
            AnimalId = animalId;
            Experimenter = experimenter;
            Notes = notes;
            Protocol = protocol;
            PiHost = piHost;

            string timeText = StartTime.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string folderName = timeText + "_" + MakeSafeFileNamePart(animalId) + "_" + protocol.Name;
            Folder = Path.Combine(Path.GetFullPath(dataFolder), folderName);

            stopwatch = Stopwatch.StartNew();
        }

        /// <summary>Gets the folder containing all files of this session.</summary>
        public string Folder { get; private set; }

        /// <summary>Gets the identifier of the animal.</summary>
        public string AnimalId { get; private set; }

        /// <summary>Gets the name of the experimenter.</summary>
        public string Experimenter { get; private set; }

        /// <summary>Gets the free-text notes.</summary>
        public string Notes { get; private set; }

        /// <summary>Gets the protocol played during the session.</summary>
        public ProtocolDefinition Protocol { get; private set; }

        /// <summary>Gets the host name of the Pi.</summary>
        public string PiHost { get; private set; }

        /// <summary>Gets the PC time at which the session started.</summary>
        public DateTimeOffset StartTime { get; private set; }

        /// <summary>Gets the PC time at which the session stopped, or null while running.</summary>
        public DateTimeOffset? StopTime { get; private set; }

        /// <summary>Gets why the session stopped (e.g. "stop button", "protocol_end").</summary>
        public string StopReason { get; private set; }

        /// <summary>Gets the seconds elapsed since the session started (PC stopwatch).</summary>
        public double ElapsedSeconds
        {
            get { return stopwatch.Elapsed.TotalSeconds; }
        }

        /// <summary>Gets the path of the raw camera video.</summary>
        public string VideoPath { get { return Path.Combine(Folder, "video.avi"); } }

        /// <summary>Gets the path of the per-frame camera log.</summary>
        public string CameraFramesPath { get { return Path.Combine(Folder, "camera_frames.csv"); } }

        /// <summary>Gets the path of the tracking log.</summary>
        public string TrackingPath { get { return Path.Combine(Folder, "tracking.csv"); } }

        /// <summary>Gets the path of the Pi message log.</summary>
        public string PiMessagesPath { get { return Path.Combine(Folder, "pi_messages.csv"); } }

        /// <summary>Gets the path of the protocol copy.</summary>
        public string ProtocolPath { get { return Path.Combine(Folder, "protocol.json"); } }

        /// <summary>Gets the path of the session metadata file.</summary>
        public string SessionInfoPath { get { return Path.Combine(Folder, "session.json"); } }

        /// <summary>
        /// Creates the session folder and writes protocol.json and session.json.
        /// </summary>
        internal void CreateFolder()
        {
            if (Directory.Exists(Folder))
            {
                throw new IOException("The session folder already exists: " + Folder);
            }
            Directory.CreateDirectory(Folder);
            string protocolJson = ProtocolJson.Serialize(Protocol);
            File.WriteAllText(ProtocolPath, protocolJson, new UTF8Encoding(false));
            WriteSessionInfo();
        }

        /// <summary>
        /// Marks the session as stopped and updates session.json.
        /// </summary>
        internal void MarkStopped(string reason)
        {
            StopTime = DateTimeOffset.Now;
            StopReason = reason;
            stopwatch.Stop();
            WriteSessionInfo();
        }

        /// <summary>
        /// Formats the current time as the two time columns shared by every CSV
        /// file: "t_session_s,pc_time".
        /// </summary>
        internal string FormatTimeColumns()
        {
            string sessionSeconds = ElapsedSeconds.ToString("0.000000", CultureInfo.InvariantCulture);
            string pcTime = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
            return sessionSeconds + "," + pcTime;
        }

        void WriteSessionInfo()
        {
            JObject info = new JObject();
            info["animal_id"] = AnimalId;
            info["experimenter"] = Experimenter;
            info["notes"] = Notes;
            info["protocol"] = Protocol.Name;
            info["protocol_duration_s"] = Protocol.TotalDurationSeconds;
            info["pi_host"] = PiHost;
            info["start_time"] = StartTime.ToString("o", CultureInfo.InvariantCulture);
            if (StopTime.HasValue)
            {
                info["stop_time"] = StopTime.Value.ToString("o", CultureInfo.InvariantCulture);
                info["duration_s"] = ElapsedSeconds;
                info["stop_reason"] = StopReason;
            }
            info["computer"] = Environment.MachineName;
            info["software"] = "Buridan.Bonsai " + typeof(ExperimentSession).Assembly.GetName().Version;
            string text = info.ToString(Formatting.Indented);
            File.WriteAllText(SessionInfoPath, text, new UTF8Encoding(false));
        }

        static string MakeSafeFileNamePart(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "unknown";
            }
            StringBuilder safe = new StringBuilder();
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char character in text)
            {
                bool isInvalid = Array.IndexOf(invalid, character) >= 0;
                if (isInvalid || character == ' ')
                {
                    safe.Append('-');
                }
                else
                {
                    safe.Append(character);
                }
            }
            return safe.ToString();
        }

        /// <summary>Returns the session folder.</summary>
        public override string ToString()
        {
            return Folder;
        }
    }
}
