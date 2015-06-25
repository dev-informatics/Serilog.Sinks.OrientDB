using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using static System.String;

namespace Serilog.Sinks.OrientDB
{
    /// <summary>
    /// The sink or connector for Serilog to OrientDB
    /// </summary>
    public class OrientSink : PeriodicBatchingSink
    {
        /// <summary>
        /// The default batch posting limit
        /// </summary>
        public const int DefaultBatchPostingLimit = 1000;
        /// <summary>
        /// The bulk upload resource path
        /// </summary>
        public const string BulkUploadResourcePath = "batch";
        /// <summary>
        /// The class path
        /// </summary>
        public const string ClassPath = "class";
        /// <summary>
        /// The property path
        /// </summary>
        public const string PropertyPath = "property";
        /// <summary>
        /// The default period to batch
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        private readonly HttpClient Client;
        /// <summary>
        /// The database name
        /// </summary>
        public readonly string Database;
        /// <summary>
        /// The class name
        /// </summary>
        public readonly string ClassName;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrientSink"/> class.
        /// </summary>
        /// <param name="serverUrl">The server URL.</param>
        /// <param name="database">The database name.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="batchSizeLimit">The batch size limit.</param>
        /// <param name="period">The period to batch.</param>
        /// <param name="className">Name of the class.</param>
        /// <exception cref="ArgumentNullException">
        /// </exception>
        public OrientSink(string serverUrl, string database, string userName = null, string password = null,
            int batchSizeLimit = DefaultBatchPostingLimit, TimeSpan? period = null, string className = "LogEvent")
            : base(batchSizeLimit, period ?? DefaultPeriod)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (database == null) throw new ArgumentNullException(nameof(database));

            Database = database;

            ClassName = IsNullOrWhiteSpace(className) ? "LogEvent" : className;

            var uriString = serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/";

            var authenticationHandler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(userName, password)
            };

            Client = new HttpClient(authenticationHandler)
            {
                BaseAddress = new Uri(uriString)
            };

            Client.DefaultRequestHeaders.ExpectContinue = false;
        }

        /// <summary>
        /// Connects and defines the class if it does not exist.
        /// </summary>
        /// <param name="serverUrl">The server URL.</param>
        /// <param name="database">The database.</param>
        /// <param name="userName">Name of the user.</param>
        /// <param name="password">The password.</param>
        /// <param name="batchSizeLimit">The batch size limit.</param>
        /// <param name="period">The period of teh batch.</param>
        /// <param name="className">Name of the class.</param>
        /// <returns>Task&lt;OrientSink&gt;.</returns>
        public static async Task<OrientSink> ConnectAndDefineClassIfNotExists(string serverUrl, string database, string userName = null, string password = null,
            int batchSizeLimit = DefaultBatchPostingLimit, TimeSpan? period = null, string className = "LogEvent")
        {
            var sink = new OrientSink(serverUrl, database, userName, password, batchSizeLimit, period, className);

            if (await sink.CheckIfClassExists()) return sink;

            await sink.CreateClass();
            await sink.AddProperties();

            return sink;
        }

        /// <summary>
        /// Checks if class exists.
        /// </summary>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        protected virtual async Task<bool> CheckIfClassExists()
        {
            bool exists = false;

            try
            {
                var result = await Client.GetAsync($"{ClassPath}/{Database}/{ClassName}");

                exists = result.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("OrientSink: Failed to check if class exists. Exception: {0}", ex);
            }

            return exists;
        }

        /// <summary>
        /// Creates the class.
        /// </summary>
        /// <returns>Task.</returns>
        /// <exception cref="LoggingFailedException">
        /// </exception>
        protected virtual async Task CreateClass()
        {
            try
            {
                var result = await Client.PostAsync($"{ClassPath}/{Database}/{ClassName}", new StringContent(Empty));

                if(!result.IsSuccessStatusCode)
                    throw new LoggingFailedException(
                        $"OrientSink: Failed to create class. StatusCode: {result.StatusCode} Body: {await result.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("OrientSink: Failed to create class. Exception: {0}", ex);
            }
        }

        /// <summary>
        /// Adds the properties.
        /// </summary>
        /// <returns>Task.</returns>
        /// <exception cref="LoggingFailedException">
        /// </exception>
        protected virtual async Task AddProperties()
        {
            try
            {
                var result = await Client.PostAsync($"{PropertyPath}/{Database}/{ClassName}", 
                    new StringContent(JsonData.LoggingClassPropertyJson));

                if (!result.IsSuccessStatusCode)
                    throw new LoggingFailedException(
                        $"OrientSink: Failed to define schema for class. StatusCode: {result.StatusCode} Body: {await result.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("OrientSink: Failed to define schema for class. Exception: {0}", ex);
            }
        }

        /// <summary>
        /// Adds the properties to class.
        /// </summary>
        /// <returns>Task.</returns>
        protected virtual async Task AddPropertiesToClass()
        {
            var result = await Client.PostAsync($"{ClassPath}/{Database}/{ClassName}", new StringContent(Empty));
        }

        /// <summary>
        /// emit batch as an asynchronous operation.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <returns>Task.</returns>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            var payload = BuildPayload(events);

            await SendBatch(payload);
        }

        /// <summary>
        /// Sends the batch.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>Task.</returns>
        /// <exception cref="LoggingFailedException">
        /// </exception>
        protected virtual async Task SendBatch(string payload)
        {
            while (true)
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var result = await Client.PostAsync($"{BulkUploadResourcePath}/{Database}", content);

                if (!result.IsSuccessStatusCode)
                {
                    if (result.StatusCode != HttpStatusCode.Unauthorized)
                        throw new LoggingFailedException(
                            $"Received failed result {result.StatusCode} when posting events to OrientDB.");
                    continue;
                }

                break;
            }
        }

        /// <summary>
        /// Builds the payload.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <returns>System.String.</returns>
        protected virtual string BuildPayload(IEnumerable<LogEvent> events)
        {
            using (var payload = new StringWriter())
            {
                payload.Write("{ \"transaction\" : false,");
                payload.Write("\"operations\" : [");


                var formatter = new OrientJsonFormatter(closingDelimiter: Empty, renderMessage: true, formatProvider: CultureInfo.GetCultureInfo("en-us"));
                var delimStart = Empty;

                foreach (var logEvent in events)
                {
                    payload.Write(delimStart);
                    payload.Write("{ \"type\" : \"c\",\"record\" : ");

                    using (var logEventJson = new StringWriter())
                    {
                        formatter.Format(logEvent, logEventJson);
                        var json = logEventJson.ToString();
                        json = json.Insert(json.IndexOf("{", StringComparison.OrdinalIgnoreCase) + 1, "\"@class\" : \"LogEvent\",");
                        payload.Write(json);
                    }

                    payload.Write("}");
                    delimStart = ",";
                }

                payload.Write("]}");

                return payload.ToString();
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                Client.Dispose();
        }
    }
}