// =============================================================================
// ProtocolOperators.cs
//
// Operators used by the GUI to list protocol files and to save protocols
// created in the protocol editor (on the PC and on the Pi).
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
    /// Represents an operator that lists the protocol files in a folder.
    /// </summary>
    [Description("Lists the names of the protocol files (*.json) in the protocol folder on every input.")]
    public class ListProtocolFiles : Combinator<string[]>
    {
        /// <summary>Creates the operator.</summary>
        public ListProtocolFiles()
        {
            ProtocolFolder = "protocols";
        }

        /// <summary>Gets or sets the folder containing the protocol files.</summary>
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        [Description("Folder on the PC containing the protocol files. Relative paths start at the workflow folder.")]
        public string ProtocolFolder { get; set; }

        /// <summary>Lists the protocol names once per input.</summary>
        public override IObservable<string[]> Process<TSource>(IObservable<TSource> source)
        {
            return source.Select(input =>
            {
                return ProtocolFiles.ListNames(ProtocolFolder);
            });
        }
    }

    /// <summary>
    /// Describes the outcome of saving a protocol.
    /// </summary>
    public class ProtocolSaveResult
    {
        /// <summary>Gets or sets whether the protocol was saved successfully everywhere requested.</summary>
        public bool Ok { get; set; }

        /// <summary>Gets or sets a human-readable description of what happened.</summary>
        public string Message { get; set; }

        /// <summary>Gets or sets the saved protocol (a copy of the editor contents).</summary>
        public ProtocolDefinition Protocol { get; set; }

        /// <summary>Gets or sets the path of the file written on the PC, if any.</summary>
        public string LocalPath { get; set; }

        /// <summary>Gets or sets the PC time of the save.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Returns the message prefixed with the time.</summary>
        public override string ToString()
        {
            string time = Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            return "[" + time + "] " + Message;
        }
    }

    /// <summary>
    /// Represents the protocol editor: holds the protocol being edited and
    /// saves it to the PC (and optionally to the Pi) on every input.
    /// </summary>
    [Description("Holds the protocol being edited. On every input, saves it to the PC protocol folder and uploads it to the Pi.")]
    public class SaveProtocol : Combinator<ProtocolSaveResult>
    {
        /// <summary>Creates the operator with a default protocol.</summary>
        public SaveProtocol()
        {
            Protocol = new ProtocolDefinition();
            ProtocolFolder = "protocols";
            UploadToPi = true;
            Connection = CreatePiConnection.DefaultName;
        }

        /// <summary>Gets or sets the protocol being edited.</summary>
        [Description("The protocol being edited. Expand it to set the name, frame rate and epochs.")]
        public ProtocolDefinition Protocol { get; set; }

        /// <summary>Gets or sets the folder on the PC where protocols are saved.</summary>
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        [Description("Folder on the PC where protocols are saved. Relative paths start at the workflow folder.")]
        public string ProtocolFolder { get; set; }

        /// <summary>Gets or sets whether the protocol is also saved on the Pi.</summary>
        [Description("If true, the protocol is also saved on the Raspberry Pi.")]
        public bool UploadToPi { get; set; }

        /// <summary>Gets or sets the name of the Pi connection to use.</summary>
        [Description("Name of the CreatePiConnection to use.")]
        public string Connection { get; set; }

        /// <summary>Saves the current protocol once per input.</summary>
        public override IObservable<ProtocolSaveResult> Process<TSource>(IObservable<TSource> source)
        {
            return source.ObserveOn(Scheduler.Default).Select(input =>
            {
                return Save();
            });
        }

        ProtocolSaveResult Save()
        {
            ProtocolSaveResult result = new ProtocolSaveResult();
            result.Timestamp = DateTimeOffset.Now;

            // Work on a copy, so later edits do not change what was saved.
            ProtocolDefinition protocol;
            try
            {
                protocol = Protocol.Copy();
                protocol.Validate();
            }
            catch (Exception error)
            {
                result.Ok = false;
                result.Message = "Protocol NOT saved: " + error.Message;
                return result;
            }
            result.Protocol = protocol;

            // 1) Save on the PC.
            try
            {
                result.LocalPath = ProtocolFiles.Save(ProtocolFolder, protocol);
            }
            catch (Exception error)
            {
                result.Ok = false;
                result.Message = "Protocol NOT saved on the PC: " + error.Message;
                return result;
            }

            // 2) Save on the Pi.
            if (!UploadToPi)
            {
                result.Ok = true;
                result.Message = "Saved '" + protocol.Name + "' on the PC (" + result.LocalPath + ").";
                return result;
            }
            PiReply reply = UploadPiProtocol.Upload(Connection, protocol);
            if (reply.Ok)
            {
                result.Ok = true;
                result.Message = "Saved '" + protocol.Name + "' on the PC and on the Pi.";
            }
            else
            {
                result.Ok = false;
                result.Message = "Saved '" + protocol.Name + "' on the PC, but NOT on the Pi: " + reply.Error;
            }
            return result;
        }
    }
}
