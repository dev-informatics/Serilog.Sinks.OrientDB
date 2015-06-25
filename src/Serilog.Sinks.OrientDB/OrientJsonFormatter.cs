using System;
using System.IO;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.OrientDB
{
    /// <summary>
    /// Specialized JSON formatting for OrientDB
    /// </summary>
    public class OrientJsonFormatter : FlexibleJsonFormatter
    {
        /// <summary>
        /// Creates an instance of the OrientDb JSON formatter.
        /// </summary>
        /// <param name="omitEnclosingObject">
        ///     If true, the properties of the event will be written to the output without enclosing 
        ///     braces. Otherwise, if false, each event will be written as a well-formed JSON object.
        /// </param>
        /// <param name="closingDelimiter">
        ///     A string that will be written after each log event is formatted. If null, System.Environment.NewLine
        ///     will be used. Ignored if omitEnclosingObject is true.
        /// </param>
        /// <param name="renderMessage">
        ///     If true, the message will be rendered and written to the output as a property
        ///     named RenderedMessage.
        /// </param>
        /// <param name="formatProvider">
        ///     Supplies culture-specific formatting information, or null.
        /// </param>
        public OrientJsonFormatter(
            bool omitEnclosingObject = false,
            string closingDelimiter = null,
            bool renderMessage = false,
            IFormatProvider formatProvider = null)
            : base(omitEnclosingObject, closingDelimiter, renderMessage, formatProvider)
        {   
        }

        /// <summary>
        /// Writes the date time.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected override void WriteDateTime(DateTime value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff"));
            output.Write("\"");
        }

        /// <summary>
        /// Writes the date time offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected override void WriteOffset(DateTimeOffset value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff"));
            output.Write("\"");
        }
    }
}
