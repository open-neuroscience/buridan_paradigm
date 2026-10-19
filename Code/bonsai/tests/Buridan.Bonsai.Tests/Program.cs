// =============================================================================
// Program.cs — Buridan.Bonsai self-tests
//
// Part 1 (always): protocol model, JSON format and protocol files.
// Part 2 (when a host is given): operators talking to a running buridan_server:
//   commands, status/event messages, connection monitor, and a complete
//   experiment session (start -> protocol_end -> stop) with its files.
//
// Usage: dotnet run -- <path to pi/protocols> [pi host]
// Exit code 0 if every check passes.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Buridan.Bonsai;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai.Tests
{
    static class Program
    {
        static int passed;
        static int failed;

        static void Check(bool condition, string description)
        {
            if (condition)
            {
                passed += 1;
                Console.WriteLine("[PASS] " + description);
            }
            else
            {
                failed += 1;
                Console.WriteLine("[FAIL] " + description);
            }
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("usage: dotnet run -- <path to pi/protocols> [pi host]");
                return 2;
            }
            string piProtocolFolder = args[0];
            string host = null;
            if (args.Length >= 2)
            {
                host = args[1];
            }

            string workFolder = Path.Combine(Path.GetTempPath(), "buridan_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workFolder);
            try
            {
                TestProtocolModel();
                TestPiProtocolFiles(piProtocolFolder, workFolder);
                if (host != null)
                {
                    TestNetwork(host, workFolder);
                }
                else
                {
                    Console.WriteLine("(no host given: network tests skipped)");
                }
            }
            catch (Exception error)
            {
                failed += 1;
                Console.WriteLine("[FAIL] unexpected exception: " + error);
            }
            finally
            {
                Directory.Delete(workFolder, true);
            }

            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------------
        // Part 1: protocol model
        // ---------------------------------------------------------------------

        static void TestProtocolModel()
        {
            ProtocolDefinition protocol = new ProtocolDefinition();
            protocol.Name = "model_test";
            protocol.Epochs.Clear();

            EpochDefinition stripes = new EpochDefinition();
            stripes.Type = StimulusType.Stripes;
            stripes.DurationSeconds = 10;
            stripes.StripeCentersDeg = new double[] { 45, 225 };
            stripes.StripeColor = Color.FromArgb(10, 20, 30);
            protocol.Epochs.Add(stripes);

            EpochDefinition grating = new EpochDefinition();
            grating.Type = StimulusType.Grating;
            grating.DurationSeconds = 5;
            grating.Direction = RotationDirection.CounterClockwise;
            grating.Waveform = GratingWaveform.Sine;
            protocol.Epochs.Add(grating);

            protocol.Validate();
            Check(true, "valid protocol passes validation");
            Check(protocol.TotalDurationSeconds == 15, "total duration is the sum of epochs");

            string json = ProtocolJson.Serialize(protocol);
            JObject document = JObject.Parse(json);
            Check((string)document["epochs"][0]["type"] == "stripes", "stimulus type written as 'stripes'");
            Check((int)document["epochs"][0]["params"]["stripe_rgb"][2] == 30, "colour written as [r, g, b]");
            Check((string)document["epochs"][1]["params"]["direction"] == "ccw", "direction written as 'ccw'");
            Check(document["epochs"][0]["params"]["period_deg"] == null, "only parameters of the epoch type are written");

            ProtocolDefinition copy = protocol.Copy();
            string copyJson = ProtocolJson.Serialize(copy);
            Check(copyJson == json, "JSON round trip is lossless");

            // Missing parameters take buridan_server's defaults.
            string minimal = "{\"name\":\"minimal\",\"epochs\":[{\"type\":\"grating\",\"duration_s\":3}]}";
            ProtocolDefinition parsed = ProtocolJson.Deserialize(minimal);
            Check(parsed.Epochs[0].SpeedDegPerSecond == 0.0, "missing speed_deg_s defaults to 0 (as on the Pi)");
            Check(parsed.Epochs[0].PeriodDeg == 30.0, "missing period_deg defaults to 30 (as on the Pi)");

            Check(Throws(() => { ProtocolDefinition bad = new ProtocolDefinition(); bad.Name = "../evil"; bad.Validate(); }),
                  "unsafe protocol name is rejected");
            Check(Throws(() => { ProtocolDefinition bad = new ProtocolDefinition(); bad.Epochs[0].DurationSeconds = 0; bad.Validate(); }),
                  "zero duration is rejected");
            Check(Throws(() => { ProtocolDefinition bad = new ProtocolDefinition(); bad.Epochs[0].Type = StimulusType.Grating; bad.Epochs[0].DutyCycle = 2; bad.Validate(); }),
                  "duty cycle above 1 is rejected");
            Check(Throws(() => ProtocolJson.Deserialize("{\"name\":\"x\",\"epochs\":[{\"type\":\"spiral\",\"duration_s\":1}]}")),
                  "unknown stimulus type is rejected");
        }

        static void TestPiProtocolFiles(string piProtocolFolder, string workFolder)
        {
            // Every example protocol shipped for the Pi must load on the PC,
            // and saving it again must keep its meaning.
            string[] names = ProtocolFiles.ListNames(piProtocolFolder);
            Check(names.Length >= 3, "example protocols are listed (" + names.Length + ")");

            string saveFolder = Path.Combine(workFolder, "protocols");
            foreach (string name in names)
            {
                ProtocolDefinition protocol = ProtocolFiles.Load(piProtocolFolder, name);
                protocol.Validate();
                string savedPath = ProtocolFiles.Save(saveFolder, protocol);
                ProtocolDefinition reloaded = ProtocolFiles.Load(saveFolder, name);
                bool same = ProtocolJson.Serialize(reloaded) == ProtocolJson.Serialize(protocol);
                Check(same && File.Exists(savedPath), "example protocol '" + name + "' loads, saves and reloads");
            }
            string[] savedNames = ProtocolFiles.ListNames(saveFolder);
            Check(savedNames.SequenceEqual(names), "saved protocols are listed in the same order");
        }

        // ---------------------------------------------------------------------
        // Part 2: network and sessions
        // ---------------------------------------------------------------------

        static void TestNetwork(string host, string workFolder)
        {
            CreatePiConnection createConnection = new CreatePiConnection();
            createConnection.Host = host;
            using (createConnection.Generate().Subscribe())
            {
                PiReply reply = Wait(new GetPiStatus().Process(Observable.Return(1)));
                Check(reply.Ok, "GetPiStatus: " + reply);

                PiReply badReply = Wait(new SendPiCommand().Process(Observable.Return("{\"cmd\":\"nonsense\"}")));
                Check(!badReply.Ok && badReply.Error.Contains("unknown command"), "unknown command reported as failure");

                PiConnectionState state = Wait(new MonitorPiConnection().Generate());
                Check(state.IsConnected, "MonitorPiConnection reports connected: " + state.ToString().Replace(Environment.NewLine, " | "));

                // A short protocol saved through the editor operator (PC + Pi).
                string protocolFolder = Path.Combine(workFolder, "session_protocols");
                SaveProtocol editor = new SaveProtocol();
                editor.ProtocolFolder = protocolFolder;
                editor.Protocol.Name = "network_test";
                editor.Protocol.Epochs.Clear();
                EpochDefinition first = new EpochDefinition();
                first.Type = StimulusType.Stripes;
                first.DurationSeconds = 0.6;
                editor.Protocol.Epochs.Add(first);
                EpochDefinition second = new EpochDefinition();
                second.Type = StimulusType.Grating;
                second.DurationSeconds = 0.6;
                editor.Protocol.Epochs.Add(second);
                ProtocolSaveResult saveResult = Wait(editor.Process(Observable.Return(1)));
                Check(saveResult.Ok, "SaveProtocol saves on PC and Pi: " + saveResult);

                JObject listRequest = PiCommandSender.CreateRequest("list_protocols");
                PiReply listReply = PiCommandSender.Send(CreatePiConnection.DefaultName, listRequest);
                bool onPi = listReply.Ok && listReply.Body["protocols"].Values<string>().Contains("network_test");
                Check(onPi, "uploaded protocol is listed on the Pi");

                string[] localNames = Wait(new ListProtocolFiles { ProtocolFolder = protocolFolder }.Process(Observable.Return(1)));
                Check(localNames.Contains("network_test"), "ListProtocolFiles lists the saved protocol");

                TestSession(protocolFolder, workFolder);

                PiReply testPattern = Wait(new SetPiMode { Mode = PiDisplayMode.TestPattern }.Process(Observable.Return(1)));
                Check(testPattern.Ok, "SetPiMode TestPattern");
                PiReply stop = Wait(new StopPi().Process(Observable.Return(1)));
                Check(stop.Ok, "StopPi");

                JObject deleteRequest = PiCommandSender.CreateRequest("delete_protocol");
                deleteRequest["name"] = "network_test";
                PiCommandSender.Send(CreatePiConnection.DefaultName, deleteRequest);
            }

            PiReply noConnection = Wait(new GetPiStatus().Process(Observable.Return(1)));
            Check(!noConnection.Ok && noConnection.Error.Contains("No Pi connection"), "missing connection reported as failure");
        }

        static void TestSession(string protocolFolder, string workFolder)
        {
            string dataFolder = Path.Combine(workFolder, "data");

            // Collect Pi messages, record them into the session, and stop the
            // session automatically on protocol_end (as the GUI workflow does).
            List<PiMessage> messages = new List<PiMessage>();
            ManualResetEventSlim protocolEnded = new ManualResetEventSlim(false);
            ExperimentStatus autoStopStatus = null;

            IObservable<PiMessage> piMessages = new ReceivePiMessages().Generate().Publish().RefCount();
            IObservable<PiMessage> recorded = new SessionPiMessageWriter().Process(piMessages);
            IObservable<ExperimentStatus> autoStop = new StopExperiment { SendStopToPi = false, Reason = "protocol_end" }
                .Process(new FilterPiEvent { EventName = "protocol_end" }.Process(piMessages));

            using (recorded.Subscribe(message => { lock (messages) { messages.Add(message); } }))
            using (autoStop.Subscribe(status => { autoStopStatus = status; protocolEnded.Set(); }))
            {
                // Give the SUB socket time to connect before the protocol starts.
                Thread.Sleep(500);

                StartExperiment start = new StartExperiment();
                start.DataFolder = dataFolder;
                start.ProtocolFolder = protocolFolder;
                start.AnimalId = "test fly";
                ExperimentStatus started = Wait(start.Process(Observable.Return("network_test")));
                Check(started.Ok, "StartExperiment: " + started);

                ExperimentStatus second = Wait(start.Process(Observable.Return("network_test")));
                Check(!second.Ok, "a second StartExperiment is refused while running");

                bool ended = protocolEnded.Wait(TimeSpan.FromSeconds(5));
                Check(ended && autoStopStatus != null && autoStopStatus.Ok, "protocol_end stops the session automatically");
                Check(SessionManager.Current == null, "no session is active after protocol_end");

                if (started.Session != null)
                {
                    ExperimentSession session = started.Session;
                    Check(session.Folder.Contains("_test-fly_network_test"), "session folder name contains animal and protocol");
                    Check(File.Exists(session.ProtocolPath), "protocol.json written");
                    JObject info = JObject.Parse(File.ReadAllText(session.SessionInfoPath));
                    Check((string)info["stop_reason"] == "protocol_end", "session.json records the stop reason");
                    string[] lines = File.ReadAllLines(session.PiMessagesPath);
                    bool hasEpochEvent = lines.Any(line => line.Contains(",event,epoch_start,"));
                    bool hasEndEvent = lines.Any(line => line.Contains(",event,protocol_end,"));
                    Check(lines.Length > 3 && hasEpochEvent && hasEndEvent,
                          "pi_messages.csv contains status and epoch/end events (" + lines.Length + " lines)");
                }
            }

            lock (messages)
            {
                int statusCount = messages.Count(message => message.Topic == "status");
                Check(statusCount >= 5, "status messages received (" + statusCount + ")");
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static T Wait<T>(IObservable<T> source)
        {
            return source.Take(1).Timeout(TimeSpan.FromSeconds(10)).Wait();
        }

        static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
