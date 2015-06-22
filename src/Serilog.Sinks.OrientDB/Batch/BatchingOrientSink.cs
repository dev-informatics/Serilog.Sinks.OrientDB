using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;
using static System.String;

namespace Serilog.Sinks.OrientDB.Batch
{
    public class OrientSink : PeriodicBatchingSink
    {
        public const int DefaultBatchPostingLimit = 1000;
        public const string BulkUploadResourcePath = "batch";
        protected const string OSessionHeader = "OSESSIONID";
        protected const string AuthenticationHeader = "Authorization";
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        private readonly HttpClient Client;
        private readonly string Credentials;
        public readonly string Database;

        public OrientSink(string serverUrl, string database, string userName = null, string password = null,
            int batchSizeLimit = DefaultBatchPostingLimit, TimeSpan? period = null)
            : base(batchSizeLimit, period ?? DefaultPeriod)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (database == null) throw new ArgumentNullException(nameof(database));

            Database = database;

            if (IsNullOrWhiteSpace(userName) || password == null)
                Credentials = null;
            else
                Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

            var uriString = serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/";

            Client = new HttpClient
            {
                BaseAddress = new Uri(uriString)
            };
        }

        protected string OSessionId { get; set; } = null;

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

                if (!IsNullOrWhiteSpace(OSessionId))
                    content.Headers.Add(OSessionHeader, OSessionId);
                else if (Credentials != null)
                    content.Headers.Add(AuthenticationHeader, $"Basic {Credentials}");

                var result = await Client.PostAsync($"{BulkUploadResourcePath}/{Database}", content);

                if (!result.IsSuccessStatusCode)
                {
                    if (result.StatusCode != HttpStatusCode.Unauthorized || OSessionId == null)
                        throw new LoggingFailedException(
                            $"Received failed result {result.StatusCode} when posting events to OrientDB.");

                    OSessionId = null;
                    continue;
                }

                IEnumerable<string> headerValues;
                result.Headers.TryGetValues(OSessionHeader, out headerValues);

                OSessionId = headerValues.FirstOrDefault();

                break;
            }
        }

        protected virtual string BuildPayload(IEnumerable<LogEvent> events)
        {
            using (var payload = new StringWriter())
            {
                payload.Write("{ \"transaction\" : false,");
                payload.Write("\r\n\t\"operations\" : [");


                var formatter = new JsonFormatter(closingDelimiter: Empty);
                var delimStart = Empty;

                foreach (var logEvent in events)
                {
                    payload.Write(delimStart);
                    payload.Write("\r\n\t\t{ \"type\" : \"c\",\r\n\t\t\t\"record\" : ");
                    formatter.Format(logEvent, payload);
                    payload.Write("\r\n\t\t}");
                    delimStart = ",";
                }

                payload.Write("\r\n\t]\r\n}");

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