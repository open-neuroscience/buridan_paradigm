// =============================================================================
// ToText.cs
//
// Converts any value into its text description (ToString). Used by the GUI
// workflow to merge the results of different operators (PiReply,
// ExperimentStatus, ProtocolSaveResult, ...) into a single log.
// =============================================================================
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Bonsai;

namespace Buridan.Bonsai
{
    /// <summary>
    /// Represents an operator that converts every value into its text description.
    /// </summary>
    [Description("Converts every value into its text description, e.g. to merge different results into one log.")]
    public class ToText : Combinator<string>
    {
        /// <summary>Converts each input value using ToString().</summary>
        public override IObservable<string> Process<TSource>(IObservable<TSource> source)
        {
            return source.Select(value =>
            {
                if (value == null)
                {
                    return string.Empty;
                }
                return value.ToString();
            });
        }
    }
}
