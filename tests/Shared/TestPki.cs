using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Squill.Tests.Pki;

/// <summary>A throw-away certificate authority generated at test time: nothing secret is ever committed.</summary>
public sealed class TestPki : IDisposable
{
    private readonly X509Certificate2 _ca;
    private readonly RSA _caKey = RSA.Create(2048);

    /// <summary>Gets the folder that holds the PEM files written by this instance.</summary>
    public string Folder { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "squill-pki-" + Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Gets the PEM text of the CA certificate.</summary>
    public string CaPem => _ca.ExportCertificatePem();

    /// <summary>Gets the path of the CA certificate file.</summary>
    public string CaFile { get; }

    /// <summary>Creates the CA and writes its certificate.</summary>
    public TestPki()
    {
        var request = new CertificateRequest("CN=squill-test-ca", _caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        _ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        CaFile = Path.Combine(Folder, "ca.pem");
        File.WriteAllText(CaFile, CaPem);
    }

    /// <summary>Issues a certificate signed by the CA.</summary>
    /// <param name="commonName">Subject common name.</param>
    /// <param name="client"><see langword="true"/> for a client certificate, <see langword="false"/> for a server one.</param>
    /// <param name="dnsNames">DNS names of the subject alternative name; the IPv4 loopback address is added unless <paramref name="loopback"/> is <see langword="false"/>.</param>
    /// <param name="fileName">Base name of the written files; defaults to the common name.</param>
    /// <param name="loopback">Whether the subject alternative name also holds 127.0.0.1.</param>
    /// <param name="keyPassphrase">Encrypts the written private key when set.</param>
    /// <returns>The PEM certificate, the PEM private key and the paths of the two files.</returns>
    public (string CertPem, string KeyPem, string CertFile, string KeyFile) Issue(string commonName, bool client, string[] dnsNames, string? keyPassphrase = null, string? fileName = null, bool loopback = true)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(client ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1")], false));
        var san = new SubjectAlternativeNameBuilder();
        foreach (var name in dnsNames)
            san.AddDnsName(name);
        if (loopback)
            san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var issued = request.Create(_ca.SubjectName, X509SignatureGenerator.CreateForRSA(_caKey, RSASignaturePadding.Pkcs1), DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(29), RandomNumberGenerator.GetBytes(8));
        var certPem = issued.ExportCertificatePem();
        var keyPem = keyPassphrase is null
            ? key.ExportPkcs8PrivateKeyPem()
            : PemEncoding.WriteString("ENCRYPTED PRIVATE KEY", key.ExportEncryptedPkcs8PrivateKey(keyPassphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000)));
        var certFile = Path.Combine(Folder, (fileName ?? commonName) + ".crt");
        var keyFile = Path.Combine(Folder, (fileName ?? commonName) + ".key");
        File.WriteAllText(certFile, certPem);
        File.WriteAllText(keyFile, keyPem);
        return (certPem, keyPem, certFile, keyFile);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ca.Dispose();
        _caKey.Dispose();
        try { Directory.Delete(Folder, true); } catch (IOException) { /* temp folder: the OS cleans it up */ }
    }
}
