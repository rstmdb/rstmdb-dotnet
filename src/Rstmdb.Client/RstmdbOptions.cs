using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Rstmdb.Client;

public sealed class RstmdbOptions
{
    /// <summary>Bearer token for authentication. Null means no auth.</summary>
    public string? Auth { get; set; }

    /// <summary>TLS options. Null means plain TCP.</summary>
    public SslClientAuthenticationOptions? Tls { get; set; }

    /// <summary>Connection dial timeout. Default: 10 seconds.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Per-request timeout. Default: 30 seconds.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Client name sent in the HELLO handshake.</summary>
    public string? ClientName { get; set; }

    /// <summary>Preferred wire mode: "binary_json" (default) or "jsonl".</summary>
    public string WireMode { get; set; } = "binary_json";

    /// <summary>Feature negotiation hints sent in HELLO.</summary>
    public string[]? Features { get; set; }

    /// <summary>
    /// Create TLS options from PEM certificate files.
    /// </summary>
    public static SslClientAuthenticationOptions CreateTls(string? caFile = null, string? certFile = null, string? keyFile = null)
    {
        var opts = new SslClientAuthenticationOptions();

        if (caFile != null)
        {
            var caCert = new X509Certificate2(caFile);
            opts.RemoteCertificateValidationCallback = (_, cert, chain, errors) =>
            {
                if (errors == SslPolicyErrors.None)
                    return true;
                if (chain != null && cert != null)
                {
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    return chain.Build(new X509Certificate2(cert));
                }
                return false;
            };
        }

        if (certFile != null && keyFile != null)
        {
            var clientCert = X509Certificate2.CreateFromPemFile(certFile, keyFile);
            opts.ClientCertificates = new X509CertificateCollection { clientCert };
        }

        return opts;
    }

    /// <summary>
    /// Create insecure TLS options that skip certificate validation. For development use only.
    /// </summary>
    public static SslClientAuthenticationOptions InsecureTls()
    {
        return new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        };
    }
}
