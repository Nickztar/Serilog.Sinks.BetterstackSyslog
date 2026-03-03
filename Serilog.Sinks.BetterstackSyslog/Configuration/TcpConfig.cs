using System;
using System.Net.Security;
using Serilog.Sinks.Syslog;

namespace Serilog.Sinks.BetterStackSyslog;

public class TcpConfig
{
    /// <summary>
    /// Defines how syslog messages are framed. Defaults to the octet-counting method described in
    /// RFC5425 and RFC6587
    /// </summary>
    public MessageFramer Framer { get; set; } = new MessageFramer(FramingType.OCTET_COUNTING);

    /// <summary>
    /// Enables the sending of keep-alive packets at the TCP socket layer. To be useful, keep-alive
    /// must be supported and enabled by the syslog server.
    /// </summary>
    /// <remarks>
    /// Defaults to false, as it's disabled by default on popular syslog servers such as rsyslog 
    /// and syslog-ng
    /// </remarks>
    public bool KeepAlive { get; set; } = false;

    /// <summary>
    /// If <c>true</c>, the connection to the Syslog server will be secured using SSL/TLS, as chosen by
    /// the operating system, while negotiating with the Syslog server. Note that the server must be
    /// configured to support TLS in order for the connection to succeed. If using .NET 4.6.2, please read
    /// the Readme.md file for additional information.
    /// If set to <c>false</c>, the sink will connect to the Syslog server over an unsecure TCP connection.
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// When <see cref="UseTls"/> is <c>true</c>, CertProvider can be used to present a client certificate
    /// to the syslog server. Leave as null if no client certificate is required
    /// </summary>
    public ICertificateProvider? CertProvider { get; set; }

    /// <summary>
    /// Callback to validate the server's SSL certificate. If null, the system default will be used
    /// </summary>
    public RemoteCertificateValidationCallback? CertValidationCallback { get; set; }

    /// <summary>
    /// When making a secure TCP connection, determines whether the server's certificate CRL, if
    /// specified in the CRL Distribution Point (CDP) extension of the certificate or any intermediate
    /// certificates, will be downloaded and checked for revocation. If any certificate within the
    /// certificate chain has been revoked, the connection will be aborted. That behavior, of course,
    /// can be customized by using the <see cref="CertValidationCallback"/> handler.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = false;

    /// <summary>
    /// A timeout value for the TCP connection to perform the TLS handshake with the server. This is
    /// only applicable if <see cref="UseTls"/> is set to <c>true</c>. This timeout value will ensure
    /// that if the server happens to not support TLS at all, for example, the connection may appear to
    /// hang, waiting for it to complete. This timeout will cause a disconnect and raise an exception
    /// after the elapsed time. The default value is 100 seconds.
    /// </summary>
    /// <remarks>This does not control the initial TCP connection timeout.</remarks>
    public TimeSpan TlsAuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(100);
    
    internal void ApplyToSource(SyslogTcpConfig source)
    {
        source.KeepAlive = KeepAlive;
        source.Framer = Framer;
        source.UseTls = UseTls;
        source.CertProvider = CertProvider;
        source.CertValidationCallback = CertValidationCallback;
        source.CheckCertificateRevocation = CheckCertificateRevocation;
        source.TlsAuthenticationTimeout = TlsAuthenticationTimeout;
    }
}