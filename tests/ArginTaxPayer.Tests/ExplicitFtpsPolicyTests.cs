using System.Net.Security;
using System.Security.Authentication;
using ArginTaxPayer.Workers.DatabaseBackup;
using FluentFTP;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ExplicitFtpsPolicyTests
{
    [Fact]
    public void Client_requires_explicit_tls_encrypted_data_and_passive_mode()
    {
        var config = ExplicitFtpsClient.CreateConfiguration();
        Assert.Equal(FtpEncryptionMode.Explicit, config.EncryptionMode);
        Assert.True(config.DataConnectionEncryption);
        Assert.Equal(FtpDataConnectionType.PASV, config.DataConnectionType);
        Assert.False(config.ValidateAnyCertificate);
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, config.SslProtocols);
    }

    [Fact]
    public void Certificate_policy_rejects_bad_chain_and_hostname_without_fallback()
    {
        Assert.True(ExplicitFtpsClient.AcceptCertificate(SslPolicyErrors.None));
        Assert.False(ExplicitFtpsClient.AcceptCertificate(SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(ExplicitFtpsClient.AcceptCertificate(SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Remote_paths_and_credentials_reject_protocol_injection()
    {
        Assert.Throws<ArgumentException>(() => new FtpsConnectionSettings("host\r\nQUIT", 21, "user", "secret").Validate());
        Assert.Throws<ArgumentException>(() => new FtpsConnectionSettings("host", 21, "user", "secret\n").Validate());
    }
}
