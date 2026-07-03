using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ErectRoom
{
    public static class CertificateUtility
    {
        /// <summary>
        /// Generates a self-signed certificate for a given hostname and adds it to the Local Machine's Trusted Root store.
        /// </summary>
        /// <param name="hostname">The domain name (e.g., "localhost" or "tmb.tabbycluster.net")</param>
        /// <returns>The generated X509Certificate2 instance.</returns>
        public static X509Certificate2 CreateAndTrustCertificate(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

            // 1. Generate the RSA key pair
            using (RSA rsa = RSA.Create(2048))
            {
                var subjectName = new X500DistinguishedName($"CN={hostname}");

                // 2. Create the certificate request
                var request = new CertificateRequest(
                    subjectName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );

                // 3. Add Enhanced Key Usage (Server Authentication is required for HTTPS)
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Auth OID
                        false
                    )
                );

                // 4. Add Subject Alternative Name (SAN) — Modern apps/browsers require this match
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName(hostname);             // rec.net
                sanBuilder.AddDnsName($"*.{hostname}");     // *.rec.net (Matches cdn.rec.net, auth.rec.net, etc.)

                if (hostname != "localhost")
                {
                    sanBuilder.AddDnsName("localhost");
                }
                request.CertificateExtensions.Add(sanBuilder.Build());
                // 5. Set Basic Constraints to identify it as an End-Entity certificate
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, false)
                );

                // 6. Create the self-signed certificate (Valid from now until 1 year from now)
                DateTimeOffset notBefore = DateTimeOffset.UtcNow;
                DateTimeOffset notAfter = notBefore.AddYears(1);

                X509Certificate2 generatedCert = request.CreateSelfSigned(notBefore, notAfter);

                // IMPORTANT: In .NET, CreateSelfSigned returns a cert that doesn't persist its private key 
                // correctly across export/stores unless converted to a PFX format in memory first.
                X509Certificate2 exportableCert = new X509Certificate2(
                    generatedCert.Export(X509ContentType.Pfx),
                    (string)null,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet
                );

                // 7. Install it into the Trusted Root Store
                InstallToRootStore(exportableCert);

                return exportableCert;
            }
        }

        private static void InstallToRootStore(X509Certificate2 certificate)
        {
            using (X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                try
                {
                    store.Open(OpenFlags.ReadWrite);

                    // Simple thumbprint match check to avoid repetitive additions
                    var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                    if (existingCerts.Count == 0)
                    {
                        store.Add(certificate);
                        Console.WriteLine($"[Success] Dynamic certificate for '{certificate.GetNameInfo(X509NameType.DnsName, false)}' trusted system-wide.");
                    }
                    else
                    {
                        Console.WriteLine("[Info] Dynamic certificate already exists in Trusted Root.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Could not add certificate to Root Store: {ex.Message}");
                    Console.WriteLine("Ensure your application is explicitly running 'As Administrator'.");
                }
                finally
                {
                    store.Close();
                }
            }
        }
    }
}