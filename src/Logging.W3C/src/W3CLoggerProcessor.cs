using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging.W3C
{
    internal class W3CLoggerProcessor : IDisposable
    {
        private const int _maxQueuedMessages = 1024;

        private readonly string _path;
        private readonly string _fileName;
        private readonly int? _maxFileSize;
        private readonly W3CLoggingFields _loggingFields;
        private bool _hasWritten;
        private string _fieldsDirective;

        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>(_maxQueuedMessages);
        private Task _outputTask;

        public W3CLoggerProcessor(IOptionsMonitor<W3CLoggerOptions> options)
        {
            var loggerOptions = options.CurrentValue;
            _path = loggerOptions.LogDirectory;
            _fileName = loggerOptions.FileName;
            _maxFileSize = loggerOptions.FileSizeLimit;
            _loggingFields = loggerOptions.LoggingFields;

            // Start W3C message queue processor
            _outputTask = Task.Run(ProcessLogQueue);
        }

        public void EnqueueMessage(string message)
        {
            // Write log directives the first time we log a message
            if (!_hasWritten)
            {
                WriteDirectives();
            }
            if (!_messageQueue.IsAddingCompleted)
            {
                try
                {
                    _messageQueue.Add(message);
                    return;
                }
                catch (InvalidOperationException) { }
            }
        }

        internal async Task WriteMessageAsync(string entry, StreamWriter streamWriter)
        {
            await streamWriter.WriteLineAsync(entry);
            await streamWriter.FlushAsync();
        }

        private async Task ProcessLogQueue()
        {
            try
            {
                var fullName = Path.Combine(_path, $"{_fileName}{Guid.NewGuid().ToString()}.log");
                using (var streamWriter = File.AppendText(fullName))
                {
                    foreach (string message in _messageQueue.GetConsumingEnumerable())
                    {
                        await WriteMessageAsync(message, streamWriter);
                    }
                }
            }
            catch
            {
                try
                {
                    _messageQueue.CompleteAdding();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _messageQueue.Dispose();
        }

        private void WriteDirectives()
        {
            if (_hasWritten)
            {
                return;
            }
            _hasWritten = true;

            Directory.CreateDirectory(_path);
            EnqueueMessage("#Version: 1.0");

            var startTimeBuilder = new StringBuilder("#Start-Date: ");
            startTimeBuilder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            EnqueueMessage(startTimeBuilder.ToString());

            EnqueueMessage(GetFieldsDirective());
        }

        private string GetFieldsDirective()
        {
            if (!String.IsNullOrEmpty(_fieldsDirective))
            {
                return _fieldsDirective;
            }

            StringBuilder sb = new StringBuilder("#Fields: ");
            if (_loggingFields.HasFlag(W3CLoggingFields.Date))
            {
                sb.Append("date ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.Time))
            {
                sb.Append("time ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ClientIpAddress))
            {
                sb.Append("c-ip ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.UserName))
            {
                sb.Append("cs-username ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ServiceName))
            {
                sb.Append("s-sitename ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ServerName))
            {
                sb.Append("s-computername ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ServerIpAddress))
            {
                sb.Append("s-ip ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ServerPort))
            {
                sb.Append("s-port ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.Method))
            {
                sb.Append("cs-method ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.UriStem))
            {
                sb.Append("cs-uri-stem ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.UriQuery))
            {
                sb.Append("cs-uri-query ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ProtocolStatus))
            {
                sb.Append("sc-status ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.BytesSent))
            {
                sb.Append("sc-bytes ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.BytesReceived))
            {
                sb.Append("cs-bytes ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.TimeTaken))
            {
                sb.Append("time-taken ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.ProtocolVersion))
            {
                sb.Append("cs-version ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.Host))
            {
                sb.Append("cs-host ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.UserAgent))
            {
                sb.Append("cs(User-Agent) ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.Cookie))
            {
                sb.Append("cs(Cookie) ");
            }
            if (_loggingFields.HasFlag(W3CLoggingFields.Referrer))
            {
                sb.Append("cs(Referrer) ");
            }

            _fieldsDirective = sb.ToString().Trim();
            return _fieldsDirective;
        }
    }
}
