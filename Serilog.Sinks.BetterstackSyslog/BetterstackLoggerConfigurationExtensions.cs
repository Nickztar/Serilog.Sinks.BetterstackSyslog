using System;
using System.Threading;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.BetterStackSyslog;
using Serilog.Sinks.PeriodicBatching;
using Serilog.Sinks.Syslog;

// ReSharper disable once CheckNamespace
namespace Serilog
{
    /// <summary>
    /// Extends Serilog configuration to write events to a logtail
    /// </summary>
    public static class BetterstackLoggerConfigurationExtensions
    {
        private static readonly PeriodicBatchingSinkOptions DefaultBatchOptions = new()
        {
            BatchSizeLimit = 1000,
            Period = TimeSpan.FromSeconds(2),
            QueueLimit = 100_000
        };

        /// <summary>
        /// Adds a sink that writes log events to a UDP syslog server
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="tokenKey">The key of Logtail token, leave empty if not overriding</param>
        /// <param name="token">Your source token from (rsyslog) logtail</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="dataName">A name for the structured data, defaults to "Parameters"</param>
        /// <param name="appName">The name of the application. Must be all printable ASCII characters. Max length 32. Defaults to the current process name</param>
        /// <param name="facility"><inheritdoc cref="Facility" path="/summary"/> Defaults to <see cref="Facility.Local0"/>.</param>
        /// <param name="batchConfig">Batching configuration</param>
        /// <param name="outputTemplate">A message template describing the output messages</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level to be changed at runtime.</param>
        /// <param name="messageIdPropertyName">Where the Id number of the message will be derived from. Defaults to the "SourceContext" property of the syslog event. Property name and value must be all printable ASCII characters with max length of 32.</param>
        /// <param name="sourceHost"><inheritdoc cref="BetterstackFormatterBase.Host" path="/summary"/></param>
        /// <param name="severityMapping">Provide your own method to override the default mapping logic of a Serilog <see cref="LogEventLevel"/></param>
        /// <param name="formatter">The message formatter</param>
        /// <param name="dnsTtl">How long a resolved IP for <paramref name="host"/> is used before resolving again. UDP gives no error when the
        /// ingestion host moves, so without this logs are silently lost. Defaults to 1 minute, <see cref="Timeout.InfiniteTimeSpan"/> resolves only once.</param>
        /// <see href="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        public static LoggerConfiguration BetterstackUdp(
            this LoggerSinkConfiguration loggerSinkConfig,
            string host, 
            int port, 
            string token,
            string tokenKey = "logtail@11993 source_token",
            string dataName = "Parameters", 
            string? appName = null,
            Facility facility = Facility.Local0, 
            PeriodicBatchingSinkOptions? batchConfig = null, 
            string? outputTemplate = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch? levelSwitch = null,
            string messageIdPropertyName = BetterstackFormatter.DefaultMessageIdPropertyName,
            string? sourceHost = null,
            Func<LogEventLevel, Severity>? severityMapping = null, 
            ITextFormatter? formatter = null,
            TimeSpan? dnsTtl = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException(nameof(host));

            batchConfig ??= DefaultBatchOptions;
            var messageFormatter = GetFormatter(tokenKey, token, dataName, appName, facility, outputTemplate, messageIdPropertyName, sourceHost, severityMapping, formatter);

            var udpSink = new ResolvingSyslogUdpSink(host, port, messageFormatter, dnsTtl ?? TimeSpan.FromMinutes(1));
            var sink = new PeriodicBatchingSink(udpSink, batchConfig);
            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        }
        
         /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured channel
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="tcpConfig">Defines how to interact with the syslog server</param>
        /// <param name="batchConfig">Batching configuration</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink.
        /// Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        public static LoggerConfiguration BetterstackTcp(
            this LoggerSinkConfiguration loggerSinkConfig,
            string host, 
            int port, 
            string token,
            string tokenKey = "logtail@11993 source_token",
            string dataName = "Parameters", 
            string? appName = null,
            Facility facility = Facility.Local0, 
            string? outputTemplate = null,
            string messageIdPropertyName = BetterstackFormatter.DefaultMessageIdPropertyName,
            string? sourceHost = null,
            TcpConfig? tcpConfig = null, 
            PeriodicBatchingSinkOptions? batchConfig = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            Func<LogEventLevel, Severity>? severityMapping = null, 
            LoggingLevelSwitch? levelSwitch = null,
            ITextFormatter? formatter = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException(nameof(host));

            batchConfig ??= DefaultBatchOptions;

            var messageFormatter = GetFormatter(tokenKey, token, dataName, appName, facility, outputTemplate, messageIdPropertyName, sourceHost, severityMapping, formatter);
            var configuration = new SyslogTcpConfig()
            {
                Host = host,
                Port = port,
                Formatter = messageFormatter,
                UseTls = true
            };
            tcpConfig?.ApplyToSource(configuration);
            var syslogTcpSink = new SyslogTcpSink(configuration);
            var sink = new PeriodicBatchingSink(syslogTcpSink, batchConfig);

            return loggerSinkConfig.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        }

        /// <summary>An alternative mapping function that can be specified in any of the 'severityMapping' parameters.
        /// This mapping takes a numerical approach, as opposed to a name approach when converting a Serilog
        /// <see cref="LogEventLevel"/> to a syslog <see cref="Severity"/>. However, since syslog has more possible
        /// values, <see cref="Severity.Critical"/> and <see cref="Severity.Alert"/> are skipped and the mapping
        /// ends with <see cref="LogEventLevel.Fatal"/> being assigned to <see cref="Severity.Emergency"/>.</summary>
        /// <param name="logEventLevel">A Serilog <see cref="LogEventLevel"/>.</param>
        /// <returns>A syslog <see cref="Severity"/>.</returns>
        public static Severity ValueBasedLogLevelToSeverityMap(LogEventLevel logEventLevel)
            => logEventLevel switch
            {
                LogEventLevel.Verbose     => Severity.Debug,
                LogEventLevel.Debug       => Severity.Informational,
                LogEventLevel.Information => Severity.Notice,
                LogEventLevel.Warning     => Severity.Warning,
                LogEventLevel.Error       => Severity.Error,
                LogEventLevel.Fatal       => Severity.Emergency,
                _ => throw new ArgumentOutOfRangeException(nameof(logEventLevel), $"The value {logEventLevel} is not a valid LogEventLevel.")
            };

        private static ISyslogFormatter GetFormatter(
            string tokenKey,
            string token,
            string dataName,
            string? appName, 
            Facility facility,
            string? outputTemplate,
            string? messageIdPropertyName,
            string? sourceHost,
            Func<LogEventLevel, Severity>? severityMapping = null, 
            ITextFormatter? formatter = null)
        {
            ITextFormatter? templateFormatter;

            if (formatter == null)
            {
                templateFormatter = string.IsNullOrWhiteSpace(outputTemplate)
                    ? null
                    : new MessageTemplateTextFormatter(outputTemplate!);
            }
            else
            {
                templateFormatter = formatter;
            }

            return new BetterstackFormatter(
                tokenKey,
                token,
                dataName,
                facility,
                appName,
                templateFormatter,
                messageIdPropertyName,
                sourceHost,
                severityMapping
            );
        }
    }
}
