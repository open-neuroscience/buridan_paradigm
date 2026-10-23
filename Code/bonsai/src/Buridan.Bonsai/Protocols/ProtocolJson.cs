// =============================================================================
// ProtocolJson.cs
//
// Converts between ProtocolDefinition objects and the protocol JSON format
// shared with buridan_server (see docs/protocol-schema.md).
//
// Writing: every parameter of the selected stimulus type is written
// explicitly, so the file never depends on default values.
// Reading: a missing parameter takes the SAME default value that
// buridan_server uses, so a file means exactly the same thing on both sides.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides conversion between protocols and their JSON representation.
    /// </summary>
    public static class ProtocolJson
    {
        // Defaults used by buridan_server when a parameter is missing.
        static readonly double[] DefaultStripeCenters = new double[] { 0.0, 180.0 };
        const double DefaultStripeWidthDeg = 11.25;
        const int DefaultStripeTopPx = 0;
        const int DefaultStripeBottomPx = 63;
        const double DefaultPeriodDeg = 30.0;
        const double DefaultSpeedDegPerSecond = 0.0;
        const double DefaultDutyCycle = 0.5;
        const double DefaultPhaseDeg = 0.0;
        const double DefaultFrameRateHz = 60.0;
        const double DefaultBarRegionCenterDeg = 0.0;
        const double DefaultRegionWidthDeg = 90.0;
        const int DefaultRegionBars = 3;
        const double DefaultRegionBarFraction = 0.25;

        /// <summary>
        /// Converts a protocol into indented JSON text.
        /// </summary>
        public static string Serialize(ProtocolDefinition protocol)
        {
            JObject document = ToJObject(protocol);
            return document.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Parses JSON text into a protocol.
        /// Throws <see cref="FormatException"/> if the text is not a valid protocol.
        /// </summary>
        public static ProtocolDefinition Deserialize(string json)
        {
            JObject document;
            try
            {
                document = JObject.Parse(json);
            }
            catch (JsonException error)
            {
                throw new FormatException("Invalid protocol JSON: " + error.Message, error);
            }
            return FromJObject(document);
        }

        /// <summary>
        /// Converts a protocol into a JSON object.
        /// </summary>
        public static JObject ToJObject(ProtocolDefinition protocol)
        {
            JObject document = new JObject();
            document["name"] = protocol.Name;
            document["schema_version"] = ProtocolDefinition.SchemaVersion;
            document["frame_rate_hz"] = protocol.FrameRateHz;

            JArray epochs = new JArray();
            if (protocol.Epochs != null)
            {
                foreach (EpochDefinition epoch in protocol.Epochs)
                {
                    JObject epochJson = EpochToJObject(epoch);
                    epochs.Add(epochJson);
                }
            }
            document["epochs"] = epochs;
            return document;
        }

        /// <summary>
        /// Converts a JSON object into a protocol.
        /// Throws <see cref="FormatException"/> if a required field is missing.
        /// </summary>
        public static ProtocolDefinition FromJObject(JObject document)
        {
            ProtocolDefinition protocol = new ProtocolDefinition();
            protocol.Epochs.Clear();

            JToken nameToken = document["name"];
            if (nameToken == null || nameToken.Type != JTokenType.String)
            {
                throw new FormatException("Protocol 'name' is missing or not a string.");
            }
            protocol.Name = nameToken.Value<string>();

            JToken versionToken = document["schema_version"];
            if (versionToken != null)
            {
                int version = versionToken.Value<int>();
                if (version != ProtocolDefinition.SchemaVersion)
                {
                    throw new FormatException("Unsupported protocol schema_version " + version + " (expected 1).");
                }
            }

            protocol.FrameRateHz = ReadDouble(document, "frame_rate_hz", DefaultFrameRateHz);

            JArray epochs = document["epochs"] as JArray;
            if (epochs == null)
            {
                throw new FormatException("Protocol 'epochs' is missing or not an array.");
            }
            for (int index = 0; index < epochs.Count; index++)
            {
                JObject epochJson = epochs[index] as JObject;
                if (epochJson == null)
                {
                    throw new FormatException("Epoch " + index + " is not a JSON object.");
                }
                EpochDefinition epoch = EpochFromJObject(epochJson, index);
                protocol.Epochs.Add(epoch);
            }

            return protocol;
        }

        // ---------------------------------------------------------------------
        // Epochs
        // ---------------------------------------------------------------------

        static JObject EpochToJObject(EpochDefinition epoch)
        {
            JObject epochJson = new JObject();
            epochJson["type"] = StimulusTypeToJson(epoch.Type);
            epochJson["duration_s"] = epoch.DurationSeconds;

            JObject parameters = new JObject();
            if (epoch.Type == StimulusType.Stripes)
            {
                parameters["centers_deg"] = new JArray(epoch.StripeCentersDeg);
                parameters["width_deg"] = epoch.StripeWidthDeg;
                parameters["stripe_rgb"] = ColorToJson(epoch.StripeColor);
                parameters["background_rgb"] = ColorToJson(epoch.BackgroundColor);
                parameters["stripe_top_px"] = epoch.StripeTopPx;
                parameters["stripe_bottom_px"] = epoch.StripeBottomPx;
            }
            else if (epoch.Type == StimulusType.Grating)
            {
                parameters["period_deg"] = epoch.PeriodDeg;
                parameters["speed_deg_s"] = epoch.SpeedDegPerSecond;
                parameters["direction"] = DirectionToJson(epoch.Direction);
                parameters["waveform"] = WaveformToJson(epoch.Waveform);
                parameters["duty_cycle"] = epoch.DutyCycle;
                parameters["phase_deg"] = epoch.PhaseDeg;
                parameters["rgb_a"] = ColorToJson(epoch.ColorA);
                parameters["rgb_b"] = ColorToJson(epoch.ColorB);
            }
            else if (epoch.Type == StimulusType.BarRegions)
            {
                parameters["center_deg"] = epoch.BarRegionCenterDeg;
                parameters["region_a_width_deg"] = epoch.RegionAWidthDeg;
                parameters["region_a_bars"] = epoch.RegionABars;
                parameters["region_a_bar_fraction"] = epoch.RegionABarFraction;
                parameters["region_b_width_deg"] = epoch.RegionBWidthDeg;
                parameters["region_b_bars"] = epoch.RegionBBars;
                parameters["region_b_bar_fraction"] = epoch.RegionBBarFraction;
                parameters["bar_rgb"] = ColorToJson(epoch.BarColor);
                parameters["background_rgb"] = ColorToJson(epoch.BarBackgroundColor);
            }
            else
            {
                parameters["rgb"] = ColorToJson(epoch.BlankColor);
            }
            epochJson["params"] = parameters;
            return epochJson;
        }

        static EpochDefinition EpochFromJObject(JObject epochJson, int index)
        {
            string where = "Epoch " + index + ": ";
            EpochDefinition epoch = new EpochDefinition();

            JToken typeToken = epochJson["type"];
            if (typeToken == null || typeToken.Type != JTokenType.String)
            {
                throw new FormatException(where + "'type' is missing or not a string.");
            }
            epoch.Type = StimulusTypeFromJson(typeToken.Value<string>(), where);

            JToken durationToken = epochJson["duration_s"];
            if (durationToken == null)
            {
                throw new FormatException(where + "'duration_s' is missing.");
            }
            epoch.DurationSeconds = durationToken.Value<double>();

            JObject parameters = epochJson["params"] as JObject;
            if (parameters == null)
            {
                parameters = new JObject();
            }

            if (epoch.Type == StimulusType.Stripes)
            {
                epoch.StripeCentersDeg = ReadDoubleArray(parameters, "centers_deg", DefaultStripeCenters);
                epoch.StripeWidthDeg = ReadDouble(parameters, "width_deg", DefaultStripeWidthDeg);
                epoch.StripeColor = ReadColor(parameters, "stripe_rgb", Color.Black);
                epoch.BackgroundColor = ReadColor(parameters, "background_rgb", Color.White);
                epoch.StripeTopPx = ReadInt(parameters, "stripe_top_px", DefaultStripeTopPx);
                epoch.StripeBottomPx = ReadInt(parameters, "stripe_bottom_px", DefaultStripeBottomPx);
            }
            else if (epoch.Type == StimulusType.Grating)
            {
                epoch.PeriodDeg = ReadDouble(parameters, "period_deg", DefaultPeriodDeg);
                epoch.SpeedDegPerSecond = ReadDouble(parameters, "speed_deg_s", DefaultSpeedDegPerSecond);
                epoch.Direction = DirectionFromJson(ReadString(parameters, "direction", "cw"), where);
                epoch.Waveform = WaveformFromJson(ReadString(parameters, "waveform", "square"), where);
                epoch.DutyCycle = ReadDouble(parameters, "duty_cycle", DefaultDutyCycle);
                epoch.PhaseDeg = ReadDouble(parameters, "phase_deg", DefaultPhaseDeg);
                epoch.ColorA = ReadColor(parameters, "rgb_a", Color.White);
                epoch.ColorB = ReadColor(parameters, "rgb_b", Color.Black);
            }
            else if (epoch.Type == StimulusType.BarRegions)
            {
                epoch.BarRegionCenterDeg = ReadDouble(parameters, "center_deg", DefaultBarRegionCenterDeg);
                epoch.RegionAWidthDeg = ReadDouble(parameters, "region_a_width_deg", DefaultRegionWidthDeg);
                epoch.RegionABars = ReadInt(parameters, "region_a_bars", DefaultRegionBars);
                epoch.RegionABarFraction = ReadDouble(parameters, "region_a_bar_fraction", DefaultRegionBarFraction);
                epoch.RegionBWidthDeg = ReadDouble(parameters, "region_b_width_deg", DefaultRegionWidthDeg);
                epoch.RegionBBars = ReadInt(parameters, "region_b_bars", DefaultRegionBars);
                epoch.RegionBBarFraction = ReadDouble(parameters, "region_b_bar_fraction", DefaultRegionBarFraction);
                epoch.BarColor = ReadColor(parameters, "bar_rgb", Color.Black);
                epoch.BarBackgroundColor = ReadColor(parameters, "background_rgb", Color.White);
            }
            else
            {
                epoch.BlankColor = ReadColor(parameters, "rgb", Color.Black);
            }
            return epoch;
        }

        // ---------------------------------------------------------------------
        // Enum <-> JSON string
        // ---------------------------------------------------------------------

        static string StimulusTypeToJson(StimulusType type)
        {
            switch (type)
            {
                case StimulusType.Stripes:
                    return "stripes";
                case StimulusType.Grating:
                    return "grating";
                case StimulusType.BarRegions:
                    return "bar_regions";
                default:
                    return "blank";
            }
        }

        static StimulusType StimulusTypeFromJson(string text, string where)
        {
            if (text == "stripes")
            {
                return StimulusType.Stripes;
            }
            if (text == "grating")
            {
                return StimulusType.Grating;
            }
            if (text == "bar_regions")
            {
                return StimulusType.BarRegions;
            }
            if (text == "blank")
            {
                return StimulusType.Blank;
            }
            throw new FormatException(where + "unknown stimulus type '" + text + "'.");
        }

        static string DirectionToJson(RotationDirection direction)
        {
            if (direction == RotationDirection.Clockwise)
            {
                return "cw";
            }
            return "ccw";
        }

        static RotationDirection DirectionFromJson(string text, string where)
        {
            if (text == "cw")
            {
                return RotationDirection.Clockwise;
            }
            if (text == "ccw")
            {
                return RotationDirection.CounterClockwise;
            }
            throw new FormatException(where + "'direction' must be \"cw\" or \"ccw\".");
        }

        static string WaveformToJson(GratingWaveform waveform)
        {
            if (waveform == GratingWaveform.Square)
            {
                return "square";
            }
            return "sine";
        }

        static GratingWaveform WaveformFromJson(string text, string where)
        {
            if (text == "square")
            {
                return GratingWaveform.Square;
            }
            if (text == "sine")
            {
                return GratingWaveform.Sine;
            }
            throw new FormatException(where + "'waveform' must be \"square\" or \"sine\".");
        }

        /// <summary>
        /// Returns the JSON spelling of a Pi display mode.
        /// </summary>
        public static string DisplayModeToJson(PiDisplayMode mode)
        {
            switch (mode)
            {
                case PiDisplayMode.Stream:
                    return "stream";
                case PiDisplayMode.TestPattern:
                    return "test_pattern";
                default:
                    return "blank";
            }
        }

        // ---------------------------------------------------------------------
        // Value helpers
        // ---------------------------------------------------------------------

        static JArray ColorToJson(Color color)
        {
            // The LEDs have no transparency, so alpha is ignored.
            JArray rgb = new JArray();
            rgb.Add((int)color.R);
            rgb.Add((int)color.G);
            rgb.Add((int)color.B);
            return rgb;
        }

        static Color ReadColor(JObject parameters, string key, Color defaultColor)
        {
            JArray rgb = parameters[key] as JArray;
            if (rgb == null)
            {
                return defaultColor;
            }
            if (rgb.Count != 3)
            {
                throw new FormatException("'" + key + "' must be an array [r, g, b].");
            }
            int red = rgb[0].Value<int>();
            int green = rgb[1].Value<int>();
            int blue = rgb[2].Value<int>();
            return Color.FromArgb(red, green, blue);
        }

        static double ReadDouble(JObject parameters, string key, double defaultValue)
        {
            JToken token = parameters[key];
            if (token == null)
            {
                return defaultValue;
            }
            return token.Value<double>();
        }

        static int ReadInt(JObject parameters, string key, int defaultValue)
        {
            JToken token = parameters[key];
            if (token == null)
            {
                return defaultValue;
            }
            return token.Value<int>();
        }

        static string ReadString(JObject parameters, string key, string defaultValue)
        {
            JToken token = parameters[key];
            if (token == null)
            {
                return defaultValue;
            }
            return token.Value<string>();
        }

        static double[] ReadDoubleArray(JObject parameters, string key, double[] defaultValue)
        {
            JArray values = parameters[key] as JArray;
            if (values == null)
            {
                double[] copy = (double[])defaultValue.Clone();
                return copy;
            }
            List<double> result = new List<double>();
            foreach (JToken value in values)
            {
                result.Add(value.Value<double>());
            }
            return result.ToArray();
        }
    }
}
