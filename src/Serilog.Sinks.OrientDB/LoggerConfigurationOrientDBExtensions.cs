// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Sinks.OrientDB
{
    /// <summary>
    /// Logging sink extensions for OrientDB
    /// </summary>
    public static class LoggerConfigurationOrientDbExtensions
    {
        /// <summary>
        /// Adds a sink for OrientDB to logging configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="serverUrl">The server URL.</param>
        /// <param name="database">The database name.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="batchSizeLimit">The batch size limit.</param>
        /// <param name="period">The period to batch.</param>
        /// <param name="className">Name of the class.</param>
        /// <param name="restrictedToMinimumLevel">The restricted to minimum level.</param>
        /// <returns>LoggerConfiguration.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static LoggerConfiguration OrientDB(this LoggerSinkConfiguration configuration, 
            string serverUrl, string database,
            string userName = null, string password = null,
            int batchSizeLimit = OrientSink.DefaultBatchPostingLimit,
            TimeSpan? period = null, string className = "LogEvent", LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            return configuration.Sink(
                OrientSink
                    .ConnectAndDefineClassIfNotExists(serverUrl, database, userName, password, batchSizeLimit, period, className)
                    .Result, restrictedToMinimumLevel);
        }
    }
}
