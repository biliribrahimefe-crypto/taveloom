using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Taveloom.Host.Security;

/// <summary>The host's self-signed TLS identity. Clients pin its SHA-256 fingerprint (delivered out-of-band in the pairing QR code), so no CA is involved.</summary>
public static class HostCertificate
{
    private static readonly byte[] Entropy = "Taveloom.HostCertificate.v1"u8.ToArray();

    public static X509Certificate2 LoadOrCreate(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, "host-cert.dpapi");
        if (File.Exists(path))
        {
            try
            {
                var pfx = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
                return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException) { /* unreadable (different user / corrupted): fall through and create a new identity */ }
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=Taveloom Host", key, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var created = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var bytes = created.Export(X509ContentType.Pfx);
        File.WriteAllBytes(path, ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));
        return X509CertificateLoader.LoadPkcs12(bytes, null, X509KeyStorageFlags.Exportable);
    }

    public static string Fingerprint(X509Certificate2 cert) => Convert.ToHexString(cert.GetCertHash(HashAlgorithmName.SHA256));
}
