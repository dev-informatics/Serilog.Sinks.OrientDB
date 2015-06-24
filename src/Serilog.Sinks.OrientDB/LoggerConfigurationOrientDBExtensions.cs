using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog.Configuration;

namespace Serilog.Sinks.OrientDB
{
    public static class LoggerConfigurationOrientDbExtensions
    {
        public static LoggerConfiguration OrientDB(this LoggerSinkConfiguration configuration, 
            string serverUrl, string database,
            string userName = null, string password = null,
            int batchSizeLimit = OrientSink.DefaultBatchPostingLimit,
            TimeSpan? period = null, string className = "LogEvent")
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            return configuration.Sink(
                OrientSink
                    .ConnectAndDefineClassIfNotExists(serverUrl, database, userName, password, batchSizeLimit, period, className)
                    .Result);
        }
    }
}
