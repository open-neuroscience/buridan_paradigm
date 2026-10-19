// =============================================================================
// EpochDefinition.cs
//
// One epoch of a protocol: a stimulus shown for a fixed duration.
//
// All parameters of every stimulus type are present on this class, grouped by
// category in the property grid. Only the parameters of the selected
// StimulusType are written to the protocol file.
//
// Colours are System.Drawing.Color so that the property grid shows a colour
// picker. Because XmlSerializer (used to save Bonsai workflows) cannot store
// Color, each colour has a hidden string twin (e.g. StripeColorHtml).
// =============================================================================
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Describes one epoch of a stimulus protocol.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class EpochDefinition
    {
        const string CategoryGeneral = "1. Epoch";
        const string CategoryStripes = "2. Stripes parameters";
        const string CategoryGrating = "3. Grating parameters";
        const string CategoryBlank = "4. Blank parameters";

        /// <summary>
        /// Creates an epoch with the default parameters of every stimulus type.
        /// </summary>
        public EpochDefinition()
        {
            Type = StimulusType.Stripes;
            DurationSeconds = 60.0;

            // Classic Buridan stripes: two dark stripes 180 degrees apart.
            StripeCentersDeg = new double[] { 0.0, 180.0 };
            StripeWidthDeg = 11.25;
            StripeColor = Color.Black;
            BackgroundColor = Color.White;
            StripeTopPx = 0;
            StripeBottomPx = 63;

            // Optomotor grating.
            PeriodDeg = 30.0;
            SpeedDegPerSecond = 45.0;
            Direction = RotationDirection.Clockwise;
            Waveform = GratingWaveform.Square;
            DutyCycle = 0.5;
            PhaseDeg = 0.0;
            ColorA = Color.White;
            ColorB = Color.Black;

            // Blank.
            BlankColor = Color.Black;
        }

        // ---------------------------------------------------------------------
        // General
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the kind of stimulus shown during this epoch.
        /// </summary>
        [Category(CategoryGeneral)]
        [Description("The kind of stimulus shown during this epoch. Only the parameters of this type are used.")]
        public StimulusType Type { get; set; }

        /// <summary>
        /// Gets or sets how long the epoch lasts, in seconds.
        /// </summary>
        [Category(CategoryGeneral)]
        [Description("How long the epoch lasts, in seconds.")]
        public double DurationSeconds { get; set; }

        // ---------------------------------------------------------------------
        // Stripes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the centre angle of every stripe, in degrees around the ring.
        /// </summary>
        [Category(CategoryStripes)]
        [Description("Centre angle of every stripe, in degrees (0-360) around the ring. Default: 0 and 180.")]
        public double[] StripeCentersDeg { get; set; }

        /// <summary>
        /// Gets or sets the width of each stripe, in degrees.
        /// </summary>
        [Category(CategoryStripes)]
        [Description("Width of each stripe in degrees. 1 pixel = 0.9375 degrees.")]
        public double StripeWidthDeg { get; set; }

        /// <summary>
        /// Gets or sets the colour of the stripes.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryStripes)]
        [Description("Colour of the stripes.")]
        public Color StripeColor { get; set; }

        /// <summary>
        /// Gets or sets the colour of the background behind the stripes.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryStripes)]
        [Description("Colour of the background behind the stripes.")]
        public Color BackgroundColor { get; set; }

        /// <summary>
        /// Gets or sets the first pixel row covered by the stripes.
        /// </summary>
        [Category(CategoryStripes)]
        [Description("First pixel row (0 = top) covered by the stripes.")]
        public int StripeTopPx { get; set; }

        /// <summary>
        /// Gets or sets the last pixel row covered by the stripes.
        /// </summary>
        [Category(CategoryStripes)]
        [Description("Last pixel row (63 = bottom) covered by the stripes.")]
        public int StripeBottomPx { get; set; }

        // ---------------------------------------------------------------------
        // Grating
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the spatial period of the grating, in degrees.
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Spatial period of the grating (one bright plus one dark bar), in degrees.")]
        public double PeriodDeg { get; set; }

        /// <summary>
        /// Gets or sets the rotation speed of the grating, in degrees per second.
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Rotation speed in degrees per second. 0 gives a static grating.")]
        public double SpeedDegPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the rotation direction, seen from above.
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Rotation direction, seen from above the arena.")]
        public RotationDirection Direction { get; set; }

        /// <summary>
        /// Gets or sets the brightness profile of the grating.
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Brightness profile: hard-edged bars (Square) or smooth (Sine).")]
        public GratingWaveform Waveform { get; set; }

        /// <summary>
        /// Gets or sets the fraction of each period shown in colour A (square waveform only).
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Square waveform only: fraction (0-1) of each period shown in colour A.")]
        public double DutyCycle { get; set; }

        /// <summary>
        /// Gets or sets the starting offset of the grating, in degrees.
        /// </summary>
        [Category(CategoryGrating)]
        [Description("Starting offset of the pattern, in degrees.")]
        public double PhaseDeg { get; set; }

        /// <summary>
        /// Gets or sets the first grating colour.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryGrating)]
        [Description("First grating colour (bright bars).")]
        public Color ColorA { get; set; }

        /// <summary>
        /// Gets or sets the second grating colour.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryGrating)]
        [Description("Second grating colour (dark bars).")]
        public Color ColorB { get; set; }

        // ---------------------------------------------------------------------
        // Blank
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the uniform colour of a blank epoch.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryBlank)]
        [Description("Uniform colour shown during a blank epoch.")]
        public Color BlankColor { get; set; }

        // ---------------------------------------------------------------------
        // Hidden XML twins of the colour properties (for saving workflows).
        // ---------------------------------------------------------------------

        /// <summary>Gets or sets <see cref="StripeColor"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("StripeColor")]
        public string StripeColorHtml
        {
            get { return ColorTranslator.ToHtml(StripeColor); }
            set { StripeColor = ColorTranslator.FromHtml(value); }
        }

        /// <summary>Gets or sets <see cref="BackgroundColor"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("BackgroundColor")]
        public string BackgroundColorHtml
        {
            get { return ColorTranslator.ToHtml(BackgroundColor); }
            set { BackgroundColor = ColorTranslator.FromHtml(value); }
        }

        /// <summary>Gets or sets <see cref="ColorA"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("ColorA")]
        public string ColorAHtml
        {
            get { return ColorTranslator.ToHtml(ColorA); }
            set { ColorA = ColorTranslator.FromHtml(value); }
        }

        /// <summary>Gets or sets <see cref="ColorB"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("ColorB")]
        public string ColorBHtml
        {
            get { return ColorTranslator.ToHtml(ColorB); }
            set { ColorB = ColorTranslator.FromHtml(value); }
        }

        /// <summary>Gets or sets <see cref="BlankColor"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("BlankColor")]
        public string BlankColorHtml
        {
            get { return ColorTranslator.ToHtml(BlankColor); }
            set { BlankColor = ColorTranslator.FromHtml(value); }
        }

        // ---------------------------------------------------------------------
        // Validation and display
        // ---------------------------------------------------------------------

        /// <summary>
        /// Checks the parameters of the selected stimulus type, using the same
        /// rules as buridan_server. Throws <see cref="ArgumentException"/> with a
        /// readable message if a value is invalid.
        /// </summary>
        public void Validate()
        {
            if (DurationSeconds <= 0.0)
            {
                throw new ArgumentException("DurationSeconds must be greater than 0.");
            }

            if (Type == StimulusType.Stripes)
            {
                if (StripeCentersDeg == null || StripeCentersDeg.Length == 0)
                {
                    throw new ArgumentException("StripeCentersDeg must contain at least one angle.");
                }
                if (StripeWidthDeg <= 0.0 || StripeWidthDeg > 360.0)
                {
                    throw new ArgumentException("StripeWidthDeg must be greater than 0 and at most 360.");
                }
                bool rowsValid = StripeTopPx >= 0 && StripeBottomPx <= 63 && StripeTopPx <= StripeBottomPx;
                if (!rowsValid)
                {
                    throw new ArgumentException("Stripe rows must satisfy 0 <= StripeTopPx <= StripeBottomPx <= 63.");
                }
            }
            else if (Type == StimulusType.Grating)
            {
                if (PeriodDeg <= 0.0)
                {
                    throw new ArgumentException("PeriodDeg must be greater than 0.");
                }
                if (SpeedDegPerSecond < 0.0)
                {
                    throw new ArgumentException("SpeedDegPerSecond must be 0 or positive (use Direction to reverse).");
                }
                if (DutyCycle < 0.0 || DutyCycle > 1.0)
                {
                    throw new ArgumentException("DutyCycle must be between 0 and 1.");
                }
            }
        }

        /// <summary>
        /// Returns a short description shown in the epoch list of the editor.
        /// </summary>
        public override string ToString()
        {
            return Type + " (" + DurationSeconds + " s)";
        }
    }
}
