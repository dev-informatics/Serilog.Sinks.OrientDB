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
    public class OrientSink : PeriodicBatchingSink
    {
        public const int DefaultBatchPostingLimit = 1000;
        public const string BulkUploadResourcePath = "batch";
        public const string ClassPath = "class";
        public const string PropertyPath = "property";
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        private readonly HttpClient Client;
        public readonly string Database;
        public readonly string ClassName;

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

        public static async Task<OrientSink> ConnectAndDefineClassIfNotExists(string serverUrl, string database, string userName = null, string password = null,
            int batchSizeLimit = DefaultBatchPostingLimit, TimeSpan? period = null, string className = "LogEvent")
        {
            var sink = new OrientSink(serverUrl, database, userName, password, batchSizeLimit, period, className);

            if (await sink.CheckIfClassExists()) return sink;

            await sink.CreateClass();
            await sink.AddProperties();

            return sink;
        }

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

        protected virtual async Task AddPropertiesToClass()
        {
            var result = await Client.PostAsync($"{ClassPath}/{Database}/{ClassName}", new StringContent(Empty));
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            var payload = BuildPayload(events);

            await SendBatch(payload);
        }

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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                Client.Dispose();
        }
    }
}