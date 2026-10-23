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
        const string CategoryBarRegions = "4. Bar regions parameters";
        const string CategoryBlank = "5. Blank parameters";

        // Allowance for rounding errors (same as buridan_server), so that a bar
        // of exactly one pixel is not rejected.
        const double WidthToleranceDeg = 1e-9;

        // Width of one ring pixel in degrees (384 pixels = 360 degrees).
        const double DegreesPerPixel = 360.0 / 384.0;

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

            // Bar regions: 3 bars of 10 degrees with 30 degree gaps on each side.
            BarRegionCenterDeg = 0.0;
            RegionAWidthDeg = 90.0;
            RegionABars = 3;
            RegionABarFraction = 0.25;
            RegionBWidthDeg = 90.0;
            RegionBBars = 3;
            RegionBBarFraction = 0.25;
            BarColor = Color.Black;
            BarBackgroundColor = Color.White;

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
        // Bar regions
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the centre of region A, in degrees. Region B is centred 180 degrees away.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Centre of region A, in degrees (0-360). Region B is centred exactly opposite (+180).")]
        public double BarRegionCenterDeg { get; set; }

        /// <summary>
        /// Gets or sets the width of region A, in degrees.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Width of region A, in degrees. Region A + region B must be at most 360.")]
        public double RegionAWidthDeg { get; set; }

        /// <summary>
        /// Gets or sets the number of bars in region A.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Number of bars in region A (0 = background only). The first and last bars touch the region edges.")]
        public int RegionABars { get; set; }

        /// <summary>
        /// Gets or sets the bar width as a fraction of the bar pitch in region A.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Region A: bar width as a fraction (0-1] of the pitch (bar + gap). 1 fills the region.")]
        public double RegionABarFraction { get; set; }

        /// <summary>
        /// Gets the resulting bar and gap widths of region A.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryBarRegions)]
        [Description("Resulting bar and gap widths in region A (computed from the values above).")]
        public string RegionALayout
        {
            get { return DescribeBarRegion(RegionAWidthDeg, RegionABars, RegionABarFraction); }
        }

        /// <summary>
        /// Gets or sets the width of region B, in degrees.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Width of region B, in degrees. Region A + region B must be at most 360.")]
        public double RegionBWidthDeg { get; set; }

        /// <summary>
        /// Gets or sets the number of bars in region B.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Number of bars in region B (0 = background only). The first and last bars touch the region edges.")]
        public int RegionBBars { get; set; }

        /// <summary>
        /// Gets or sets the bar width as a fraction of the bar pitch in region B.
        /// </summary>
        [Category(CategoryBarRegions)]
        [Description("Region B: bar width as a fraction (0-1] of the pitch (bar + gap). 1 fills the region.")]
        public double RegionBBarFraction { get; set; }

        /// <summary>
        /// Gets the resulting bar and gap widths of region B.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryBarRegions)]
        [Description("Resulting bar and gap widths in region B (computed from the values above).")]
        public string RegionBLayout
        {
            get { return DescribeBarRegion(RegionBWidthDeg, RegionBBars, RegionBBarFraction); }
        }

        /// <summary>
        /// Gets or sets the colour of the bars.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryBarRegions)]
        [Description("Colour of the bars (both regions).")]
        public Color BarColor { get; set; }

        /// <summary>
        /// Gets or sets the colour of everything that is not a bar.
        /// </summary>
        [XmlIgnore]
        [Category(CategoryBarRegions)]
        [Description("Colour of the background (gaps and the rest of the ring).")]
        public Color BarBackgroundColor { get; set; }

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

        /// <summary>Gets or sets <see cref="BarColor"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("BarColor")]
        public string BarColorHtml
        {
            get { return ColorTranslator.ToHtml(BarColor); }
            set { BarColor = ColorTranslator.FromHtml(value); }
        }

        /// <summary>Gets or sets <see cref="BarBackgroundColor"/> as an HTML colour string.</summary>
        [Browsable(false)]
        [XmlElement("BarBackgroundColor")]
        public string BarBackgroundColorHtml
        {
            get { return ColorTranslator.ToHtml(BarBackgroundColor); }
            set { BarBackgroundColor = ColorTranslator.FromHtml(value); }
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
            else if (Type == StimulusType.BarRegions)
            {
                ValidateBarRegion("Region A", RegionAWidthDeg, RegionABars, RegionABarFraction);
                ValidateBarRegion("Region B", RegionBWidthDeg, RegionBBars, RegionBBarFraction);
                // The regions are centred 180 degrees apart, so they overlap only
                // if their widths add up to more than 360 degrees.
                double totalWidthDeg = RegionAWidthDeg + RegionBWidthDeg;
                if (totalWidthDeg > 360.0 + WidthToleranceDeg)
                {
                    throw new ArgumentException("RegionAWidthDeg + RegionBWidthDeg must be at most 360 (the regions overlap).");
                }
            }
        }

        /// <summary>
        /// Computes the bar layout of one region. The first bar starts at the
        /// left edge of the region and the last bar ends at its right edge:
        /// N bars of width f * p and N - 1 gaps of width (1 - f) * p fill the
        /// width W, so the pitch is p = W / (N - 1 + f).
        /// Only valid for at least one bar and a fraction greater than 0.
        /// </summary>
        static void ComputeBarLayout(double widthDeg, int bars, double fraction,
                                     out double barWidthDeg, out double gapWidthDeg)
        {
            double pitchDeg = widthDeg / (bars - 1.0 + fraction);
            barWidthDeg = fraction * pitchDeg;
            gapWidthDeg = pitchDeg - barWidthDeg;
        }

        // Checks one region with the same rules as buridan_server.
        static void ValidateBarRegion(string regionName, double widthDeg, int bars, double fraction)
        {
            if (widthDeg <= 0.0 || widthDeg > 360.0)
            {
                throw new ArgumentException(regionName + ": width must be greater than 0 and at most 360 degrees.");
            }
            if (bars < 0)
            {
                throw new ArgumentException(regionName + ": the number of bars must be 0 or more.");
            }
            if (fraction <= 0.0 || fraction > 1.0)
            {
                throw new ArgumentException(regionName + ": the bar fraction must be greater than 0 and at most 1.");
            }
            if (bars == 0)
            {
                return;
            }

            double barWidthDeg;
            double gapWidthDeg;
            ComputeBarLayout(widthDeg, bars, fraction, out barWidthDeg, out gapWidthDeg);
            if (barWidthDeg + WidthToleranceDeg < DegreesPerPixel)
            {
                throw new ArgumentException(regionName + ": bars would be " + barWidthDeg.ToString("0.###") +
                    " degrees wide, less than one pixel (0.9375); use fewer bars, a larger fraction or a wider region.");
            }
            bool hasGaps = bars > 1 && fraction < 1.0;
            if (hasGaps && gapWidthDeg + WidthToleranceDeg < DegreesPerPixel)
            {
                throw new ArgumentException(regionName + ": gaps would be " + gapWidthDeg.ToString("0.###") +
                    " degrees wide, less than one pixel (0.9375); use fewer bars, a smaller fraction or a wider region.");
            }
        }

        // Text shown in the editor, e.g. "3 bars of 10 deg, gaps 30 deg".
        static string DescribeBarRegion(double widthDeg, int bars, double fraction)
        {
            if (bars == 0)
            {
                return "no bars (background only)";
            }
            if (widthDeg <= 0.0 || bars < 0 || fraction <= 0.0 || fraction > 1.0)
            {
                return "invalid values";
            }
            double barWidthDeg;
            double gapWidthDeg;
            ComputeBarLayout(widthDeg, bars, fraction, out barWidthDeg, out gapWidthDeg);
            string barsText = bars + (bars == 1 ? " bar of " : " bars of ") + barWidthDeg.ToString("0.##") + " deg";
            if (bars == 1)
            {
                return barsText + " (fills the region)";
            }
            return barsText + ", gaps " + gapWidthDeg.ToString("0.##") + " deg";
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
