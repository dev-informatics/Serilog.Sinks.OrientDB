using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;
using static System.String;

namespace Serilog.Sinks.OrientDB
{
    public class OrientSink : PeriodicBatchingSink
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private readonly HttpClient Client;

        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        public const string BulkUploadResourcePath = "batch";
        public readonly string Database;

        public OrientSink(string serverUrl, string database, int batchSizeLimit, TimeSpan period)
            : base(batchSizeLimit, period)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (database == null) throw new ArgumentNullException(nameof(database));

            Database = database;

            var uriString = serverUrl.EndsWith("/") ? serverUrl : serverUrl + "/";

            Client = new HttpClient
            {
                BaseAddress = new Uri(uriString)
            };
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            var payload = new StringWriter();
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

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            //if (!string.IsNullOrWhiteSpace(_apiKey))
            //    content.Headers.Add(ApiKeyHeaderName, _apiKey);

            var result = await Client.PostAsync($"{BulkUploadResourcePath}/{Database}", content);
            if (!result.IsSuccessStatusCode)
                throw new LoggingFailedException(
                    $"Received failed result {result.StatusCode} when posting events to OrientDB.");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                Client.Dispose();
        }
    }
}
