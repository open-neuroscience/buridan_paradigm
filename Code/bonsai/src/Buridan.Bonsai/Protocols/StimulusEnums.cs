// =============================================================================
// StimulusEnums.cs
//
// Enumerations used by protocol epochs. Their JSON spelling (used in protocol
// files and by buridan_server on the Raspberry Pi) is given in each comment.
// =============================================================================

namespace Buridan.Bonsai
{
    /// <summary>
    /// The kind of stimulus shown during an epoch.
    /// </summary>
    public enum StimulusType
    {
        /// <summary>Vertical stripes on a uniform background. JSON: "stripes".</summary>
        Stripes,

        /// <summary>A vertical grating that can rotate around the arena. JSON: "grating".</summary>
        Grating,

        /// <summary>A single uniform colour (default black). JSON: "blank".</summary>
        Blank,

        /// <summary>Two opposing regions filled with evenly spaced bars. JSON: "bar_regions".</summary>
        BarRegions
    }

    /// <summary>
    /// Direction in which a grating rotates, seen from above the arena.
    /// </summary>
    public enum RotationDirection
    {
        /// <summary>Clockwise, seen from above. JSON: "cw".</summary>
        Clockwise,

        /// <summary>Counter-clockwise, seen from above. JSON: "ccw".</summary>
        CounterClockwise
    }

    /// <summary>
    /// Brightness profile of a grating across one spatial period.
    /// </summary>
    public enum GratingWaveform
    {
        /// <summary>Hard-edged bars. JSON: "square".</summary>
        Square,

        /// <summary>Smooth sinusoidal profile. JSON: "sine".</summary>
        Sine
    }

    /// <summary>
    /// Display modes that can be selected directly on the Pi.
    /// </summary>
    public enum PiDisplayMode
    {
        /// <summary>All LEDs off. JSON: "blank".</summary>
        Blank,

        /// <summary>Show frames streamed from the PC. JSON: "stream".</summary>
        Stream,

        /// <summary>Show the panel identification pattern. JSON: "test_pattern".</summary>
        TestPattern
    }
}
