using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.OrientDB
{
    public class OrientJsonFormatter : FlexibleJsonFormater
    {
        public OrientJsonFormatter(
            bool omitEnclosingObject = false,
            string closingDelimiter = null,
            bool renderMessage = false,
            IFormatProvider formatProvider = null)
            : base(omitEnclosingObject, closingDelimiter, renderMessage, formatProvider)
        {   
        }

        protected override void WriteDateTime(DateTime value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff"));
            output.Write("\"");
        }

        protected override void WriteOffset(DateTimeOffset value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff"));
            output.Write("\"");
        }
    }
}
