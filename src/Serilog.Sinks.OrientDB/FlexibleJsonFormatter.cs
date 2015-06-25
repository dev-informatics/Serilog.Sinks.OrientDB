using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Serilog.Events;

namespace Serilog.Formatting.Json
{
    /// <summary>
    /// Formats log events in a simple JSON structure. Instances of this class are safe
    /// for concurrent access by multiple threads.
    /// </summary>
    public class FlexibleJsonFormatter : JsonFormatter
    {
        /// <summary>
        /// Value level formatter actions.
        /// </summary>
        protected readonly IDictionary<Type, Action<object, bool, TextWriter>> LiteralWriters;

        /// <summary>
        /// Construct a Serilog.Formatting.Json.FlexibleJsonFormater
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
        public FlexibleJsonFormatter(
            bool omitEnclosingObject = false,
            string closingDelimiter = null,
            bool renderMessage = false,
            IFormatProvider formatProvider = null)
            : base(omitEnclosingObject, closingDelimiter, renderMessage, formatProvider)
        {

            LiteralWriters = new Dictionary<Type, Action<object, bool, TextWriter>>
            {
                { typeof(bool), (v, q, w) => WriteBoolean((bool)v, w) },
                { typeof(char), (v, q, w) => WriteString(((char)v).ToString(), w) },
                { typeof(byte), WriteToString },
                { typeof(sbyte), WriteToString },
                { typeof(short), WriteToString },
                { typeof(ushort), WriteToString },
                { typeof(int), WriteToString },
                { typeof(uint), WriteToString },
                { typeof(long), WriteToString },
                { typeof(ulong), WriteToString },
                { typeof(float), WriteToString },
                { typeof(double), WriteToString },
                { typeof(decimal), WriteToString },
                { typeof(string), (v, q, w) => WriteString((string)v, w) },
                { typeof(DateTime), (v, q, w) => WriteDateTime((DateTime) v, w) },
                { typeof(DateTimeOffset), (v, q, w) => WriteOffset((DateTimeOffset)v, w) },
                { typeof(ScalarValue), (v, q, w) => WriteLiteral(((ScalarValue)v).Value, w, q) },
                { typeof(SequenceValue), (v, q, w) => WriteSequence(((SequenceValue)v).Elements, w) },
                { typeof(DictionaryValue), (v, q, w) => WriteDictionary(((DictionaryValue)v).Elements, w) },
                { typeof(StructureValue), (v, q, w) => WriteStructure(((StructureValue)v).TypeTag, ((StructureValue)v).Properties, w) }
            };
        }

        /// <summary>
        /// Writes the literal.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        /// <param name="forceQuotation">if set to <c>true</c> [force quotation].</param>
        protected virtual void WriteLiteral(object value, TextWriter output, bool forceQuotation = false)
        {
            if (value == null)
            {
                output.Write("null");
                return;
            }

            Action<object, bool, TextWriter> writer;
            if (LiteralWriters.TryGetValue(value.GetType(), out writer))
            {
                writer(value, forceQuotation, output);
                return;
            }

            WriteLiteralValue(value, output);
        }

        /// <summary>
        /// Writes to string.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <param name="quote">The quote.</param>
        /// <param name="output">The output.</param>
        protected virtual void WriteToString(object number, bool quote, TextWriter output)
        {
            if (quote) output.Write('"');

            var fmt = number as IFormattable;
            output.Write(fmt?.ToString(null, CultureInfo.InvariantCulture) ?? number.ToString());

            if (quote) output.Write('"');
        }

        /// <summary>
        /// Writes the boolean.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected virtual void WriteBoolean(bool value, TextWriter output)
        {
            output.Write(value ? "true" : "false");
        }

        /// <summary>
        /// Writes the date time offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected virtual void WriteOffset(DateTimeOffset value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToString("o"));
            output.Write("\"");
        }

        /// <summary>
        /// Writes the date time.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected virtual void WriteDateTime(DateTime value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToString("o"));
            output.Write("\"");
        }

        /// <summary>
        /// Writes the string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="output">The output.</param>
        protected virtual void WriteString(string value, TextWriter output)
        {
            var content = Escape(value);
            output.Write("\"");
            output.Write(content);
            output.Write("\"");
        }

        /// <summary>
        /// Writes the dictionary.
        /// </summary>
        /// <param name="elements">The elements.</param>
        /// <param name="output">The output.</param>
        protected override void WriteDictionary(IReadOnlyDictionary<ScalarValue, LogEventPropertyValue> elements, TextWriter output)
        {
            output.Write("{");
            var delim = "";
            foreach (var e in elements)
            {
                output.Write(delim);
                delim = ",";
                WriteLiteral(e.Key, output, true);
                output.Write(":");
                WriteLiteral(e.Value, output);
            }
            output.Write("}");
        }

        /// <summary>
        /// Writes a json property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="precedingDelimiter">The preceding delimiter.</param>
        /// <param name="output">The output.</param>
        protected override void WriteJsonProperty(string name, object value, ref string precedingDelimiter, TextWriter output)
        {
            output.Write(precedingDelimiter);
            output.Write("\"");
            output.Write(name);
            output.Write("\":");
            WriteLiteral(value, output);
            precedingDelimiter = ",";
        }

        /// <summary>
        /// Writes a sequence.
        /// </summary>
        /// <param name="elements">The elements.</param>
        /// <param name="output">The output.</param>
        protected override void WriteSequence(IEnumerable elements, TextWriter output)
        {
            output.Write("[");
            var delim = "";
            foreach (var value in elements)
            {
                output.Write(delim);
                delim = ",";
                WriteLiteral(value, output);
            }
            output.Write("]");
        }
    }
}
