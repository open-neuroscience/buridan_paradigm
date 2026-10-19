// =============================================================================
// ProtocolDefinition.cs
//
// A stimulus protocol: a named list of epochs played one after the other.
// This is the PC-side twin of the protocol JSON files used by buridan_server.
// See docs/protocol-schema.md for the file format.
// =============================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Describes a complete stimulus protocol.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class ProtocolDefinition
    {
        /// <summary>
        /// The protocol file format version understood by this library.
        /// </summary>
        public const int SchemaVersion = 1;

        // Same rule as buridan_server: letters, digits, '_' and '-', 1 to 64 characters.
        static readonly Regex ValidNamePattern = new Regex("^[A-Za-z0-9_-]{1,64}$");

        /// <summary>
        /// Creates a protocol with one default epoch.
        /// </summary>
        public ProtocolDefinition()
        {
            Name = "new_protocol";
            FrameRateHz = 60.0;
            Epochs = new List<EpochDefinition>();
            Epochs.Add(new EpochDefinition());
        }

        /// <summary>
        /// Gets or sets the protocol name, also used as its file name.
        /// </summary>
        [Description("Protocol name, also used as the file name (<name>.json). Letters, digits, '_' and '-' only.")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the frame rate at which the Pi renders this protocol.
        /// </summary>
        [Description("Frame rate (Hz) at which the Pi renders this protocol (1-240).")]
        public double FrameRateHz { get; set; }

        /// <summary>
        /// Gets or sets the list of epochs, played in order.
        /// </summary>
        [Description("The epochs of the protocol, played in order. Click the '...' button to add, remove or edit epochs.")]
        [XmlArrayItem("Epoch")]
        public List<EpochDefinition> Epochs { get; set; }

        /// <summary>
        /// Gets the total duration of all epochs, in seconds.
        /// </summary>
        [XmlIgnore]
        [Description("Total duration of all epochs, in seconds.")]
        public double TotalDurationSeconds
        {
            get
            {
                double total = 0.0;
                if (Epochs == null)
                {
                    return total;
                }
                foreach (EpochDefinition epoch in Epochs)
                {
                    total += epoch.DurationSeconds;
                }
                return total;
            }
        }

        /// <summary>
        /// Returns true if the name is a valid protocol (and file) name.
        /// </summary>
        public static bool IsValidName(string name)
        {
            if (name == null)
            {
                return false;
            }
            return ValidNamePattern.IsMatch(name);
        }

        /// <summary>
        /// Checks the whole protocol using the same rules as buridan_server.
        /// Throws <see cref="ArgumentException"/> with a readable message on error.
        /// </summary>
        public void Validate()
        {
            if (!IsValidName(Name))
            {
                throw new ArgumentException("Protocol name may only contain letters, digits, '_' and '-' (1-64 characters).");
            }
            if (FrameRateHz < 1.0 || FrameRateHz > 240.0)
            {
                throw new ArgumentException("FrameRateHz must be between 1 and 240.");
            }
            if (Epochs == null || Epochs.Count == 0)
            {
                throw new ArgumentException("The protocol must contain at least one epoch.");
            }
            for (int index = 0; index < Epochs.Count; index++)
            {
                EpochDefinition epoch = Epochs[index];
                if (epoch == null)
                {
                    throw new ArgumentException("Epoch " + index + " is empty.");
                }
                try
                {
                    epoch.Validate();
                }
                catch (ArgumentException error)
                {
                    throw new ArgumentException("Epoch " + index + ": " + error.Message);
                }
            }
        }

        /// <summary>
        /// Returns an independent copy of this protocol.
        /// </summary>
        public ProtocolDefinition Copy()
        {
            // A JSON round trip copies every field, including nested epochs.
            string json = ProtocolJson.Serialize(this);
            return ProtocolJson.Deserialize(json);
        }

        /// <summary>
        /// Returns a short description of the protocol.
        /// </summary>
        public override string ToString()
        {
            int epochCount = 0;
            if (Epochs != null)
            {
                epochCount = Epochs.Count;
            }
            return Name + " (" + epochCount + " epochs, " + TotalDurationSeconds + " s)";
        }
    }
}
