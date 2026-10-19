// =============================================================================
// SessionWriters.cs
//
// Sinks that record data into the current experiment session folder:
//   SessionVideoWriter      raw camera frames  -> video.avi + camera_frames.csv
//   SessionTrackingWriter   animal position    -> tracking.csv
//   SessionPiMessageWriter  Pi status / events -> pi_messages.csv
//
// They can stay connected all the time: data is only written while a session
// is running (between StartExperiment and StopExperiment), and every session
// gets its own files.
// =============================================================================
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using Bonsai;
using Bonsai.Vision;
using OpenCV.Net;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Opens a CSV file with a header line, using UTF-8 without byte order mark.
    /// </summary>
    static class CsvFile
    {
        public static StreamWriter Open(string path, string header)
        {
            StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(header);
            return writer;
        }

        /// <summary>Quotes a text value for CSV (doubling inner quotes).</summary>
        public static string Quote(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Formats a number with a dot as decimal separator.</summary>
        public static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Represents an operator that records camera frames into the current
    /// session (video.avi and camera_frames.csv).
    /// </summary>
    [Description("Records camera frames into the current experiment session (video.avi + camera_frames.csv). Frames arriving while no experiment runs are not recorded.")]
    public class SessionVideoWriter : Sink<IplImage>
    {
        /// <summary>Creates the operator.</summary>
        public SessionVideoWriter()
        {
            FrameRate = 30.0;
            FourCC = "FMP4";
        }

        /// <summary>Gets or sets the frame rate written into the video file.</summary>
        [Description("Frame rate stored in the video file. Set it to the camera frame rate. Exact frame times are in camera_frames.csv.")]
        public double FrameRate { get; set; }

        /// <summary>Gets or sets the four-character video codec code.</summary>
        [Description("Four-character code of the video codec, e.g. FMP4 (MPEG-4) or MJPG. Same codes as Bonsai's VideoWriter.")]
        public string FourCC { get; set; }

        /// <summary>Records every frame received while a session runs.</summary>
        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return Observable.Using(
                () => new SessionBoundWriter<VideoOutput>(session => new VideoOutput(session)),
                writer => source.Do(image =>
                {
                    writer.Write((output, session) => output.WriteFrame(image, session, FourCC, FrameRate));
                }));
        }

        /// <summary>
        /// The video file and the per-frame CSV of one session.
        /// </summary>
        sealed class VideoOutput : IDisposable
        {
            readonly StreamWriter framesCsv;
            OpenCV.Net.VideoWriter video;
            string videoPath;
            long frameIndex;

            public VideoOutput(ExperimentSession session)
            {
                videoPath = session.VideoPath;
                framesCsv = CsvFile.Open(session.CameraFramesPath, "frame_index,t_session_s,pc_time");
                frameIndex = 0;
            }

            public void WriteFrame(IplImage image, ExperimentSession session, string fourCC, double frameRate)
            {
                // The video writer needs the frame size, so it is created on the first frame.
                if (video == null)
                {
                    if (fourCC == null || fourCC.Length != 4)
                    {
                        throw new InvalidOperationException("FourCC must be exactly four characters, e.g. FMP4.");
                    }
                    int codec = OpenCV.Net.VideoWriter.FourCC(fourCC[0], fourCC[1], fourCC[2], fourCC[3]);
                    bool isColor = image.Channels > 1;
                    video = new OpenCV.Net.VideoWriter(videoPath, codec, frameRate, image.Size, isColor);
                }

                video.WriteFrame(image);
                string line = frameIndex.ToString(CultureInfo.InvariantCulture) + "," + session.FormatTimeColumns();
                framesCsv.WriteLine(line);
                frameIndex += 1;
            }

            public void Dispose()
            {
                if (video != null)
                {
                    video.Close();
                    video = null;
                }
                framesCsv.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents an operator that records the tracked animal position into the
    /// current session (tracking.csv).
    /// </summary>
    [Description("Records the tracked animal (e.g. the output of LargestBinaryRegion) into tracking.csv of the current experiment session.")]
    public class SessionTrackingWriter : Sink<ConnectedComponent>
    {
        const string Header = "frame_index,t_session_s,pc_time,centroid_x,centroid_y,orientation_rad,area,major_axis,minor_axis";

        /// <summary>Records one line per tracked frame while a session runs.</summary>
        public override IObservable<ConnectedComponent> Process(IObservable<ConnectedComponent> source)
        {
            return Observable.Using(
                () => new SessionBoundWriter<TrackingOutput>(session => new TrackingOutput(session)),
                writer => source.Do(component =>
                {
                    writer.Write((output, session) => output.WriteComponent(component, session));
                }));
        }

        sealed class TrackingOutput : IDisposable
        {
            readonly StreamWriter csv;
            long frameIndex;

            public TrackingOutput(ExperimentSession session)
            {
                csv = CsvFile.Open(session.TrackingPath, Header);
                frameIndex = 0;
            }

            public void WriteComponent(ConnectedComponent component, ExperimentSession session)
            {
                // When no animal is found, the centroid is NaN and the area 0.
                StringBuilder line = new StringBuilder();
                line.Append(frameIndex.ToString(CultureInfo.InvariantCulture));
                line.Append(',');
                line.Append(session.FormatTimeColumns());
                line.Append(',');
                line.Append(CsvFile.Number(component.Centroid.X));
                line.Append(',');
                line.Append(CsvFile.Number(component.Centroid.Y));
                line.Append(',');
                line.Append(CsvFile.Number(component.Orientation));
                line.Append(',');
                line.Append(CsvFile.Number(component.Area));
                line.Append(',');
                line.Append(CsvFile.Number(component.MajorAxisLength));
                line.Append(',');
                line.Append(CsvFile.Number(component.MinorAxisLength));
                csv.WriteLine(line.ToString());
                frameIndex += 1;
            }

            public void Dispose()
            {
                csv.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents an operator that records Pi status and event messages into the
    /// current session (pi_messages.csv).
    /// </summary>
    [Description("Records Pi status and event messages into pi_messages.csv of the current experiment session.")]
    public class SessionPiMessageWriter : Sink<PiMessage>
    {
        const string Header = "t_session_s,pc_time,topic,event,payload_json";

        /// <summary>Records one line per message while a session runs.</summary>
        public override IObservable<PiMessage> Process(IObservable<PiMessage> source)
        {
            return Observable.Using(
                () => new SessionBoundWriter<StreamWriter>(session => CsvFile.Open(session.PiMessagesPath, Header)),
                writer => source.Do(message =>
                {
                    writer.Write((csv, session) =>
                    {
                        string line = session.FormatTimeColumns() + "," +
                                      message.Topic + "," +
                                      message.EventName + "," +
                                      CsvFile.Quote(message.PayloadText);
                        csv.WriteLine(line);
                    });
                }));
        }
    }
}
