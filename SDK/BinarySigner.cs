using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AirGestureAI.Utilities;

namespace AirGestureAI.SDK
{
    /// <summary>
    /// Encapsulates the result of a binary integrity and signature verification check.
    /// </summary>
    public sealed class BinaryVerificationResult
    {
        /// <summary>Gets or sets whether the verification passed.</summary>
        public bool IsValid { get; set; }

        /// <summary>Gets or sets the computed SHA-256 hex digest of the file.</summary>
        public string ComputedHash { get; set; } = string.Empty;

        /// <summary>Gets or sets the expected SHA-256 hex digest (from integrity.sha256).</summary>
        public string ExpectedHash { get; set; } = string.Empty;

        /// <summary>Gets or sets whether an Authenticode signature is present.</summary>
        public bool HasAuthenticodeSignature { get; set; }

        /// <summary>Gets or sets the signer certificate subject, if available.</summary>
        public string SignerSubject { get; set; } = string.Empty;

        /// <summary>Gets or sets the signer certificate issuer, if available.</summary>
        public string SignerIssuer { get; set; } = string.Empty;

        /// <summary>Gets or sets the certificate expiration timestamp, if available.</summary>
        public string ExpirationDate { get; set; } = string.Empty;

        /// <summary>Gets or sets the state of the signature ("Signed", "Unsigned", "Expired", "Invalid").</summary>
        public string SignatureState { get; set; } = "Unsigned";

        /// <summary>Gets or sets any error that prevented verification.</summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides SHA-256 hash computation and Authenticode signature inspection
    /// for AirGesture AI application binaries and plugin assemblies.
    /// </summary>
    public static class BinarySigner
    {
        /// <summary>
        /// Computes the SHA-256 digest of the specified file.
        /// </summary>
        /// <param name="filePath">Absolute path to the binary file.</param>
        /// <returns>Uppercase hex SHA-256 string, or empty on failure.</returns>
        public static string ComputeSha256(string filePath)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
            catch (Exception ex)
            {
                Logger.Error($"BinarySigner: Failed to compute SHA-256 for '{filePath}'", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads the expected SHA-256 hash from an <c>integrity.sha256</c> sidecar file.
        /// </summary>
        /// <param name="sha256FilePath">Absolute path to the <c>.sha256</c> sidecar file.</param>
        /// <returns>Uppercase hex expected hash, or empty if not found.</returns>
        public static string ReadExpectedHash(string sha256FilePath)
        {
            try
            {
                if (!File.Exists(sha256FilePath)) return string.Empty;
                return File.ReadAllText(sha256FilePath).Trim().ToUpperInvariant();
            }
            catch (Exception ex)
            {
                Logger.Error($"BinarySigner: Failed to read expected hash from '{sha256FilePath}'", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Verifies the SHA-256 digest of a binary against a sidecar integrity file.
        /// </summary>
        /// <param name="binaryPath">Absolute path to the target binary file.</param>
        /// <param name="hashFilePath">Path to the SHA-256 sidecar file (<c>integrity.sha256</c>).</param>
        /// <returns>A structured <see cref="BinaryVerificationResult"/>.</returns>
        public static BinaryVerificationResult VerifySha256(string binaryPath, string hashFilePath)
        {
            var result = new BinaryVerificationResult();

            if (!File.Exists(binaryPath))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Binary not found at '{binaryPath}'.";
                Logger.Warn($"BinarySigner: Binary not found at '{binaryPath}'.");
                return result;
            }

            result.ComputedHash = ComputeSha256(binaryPath);
            result.ExpectedHash = ReadExpectedHash(hashFilePath);

            if (string.IsNullOrEmpty(result.ExpectedHash))
            {
                result.IsValid = false;
                result.ErrorMessage = "No integrity.sha256 sidecar file found or it is empty.";
                Logger.Warn($"BinarySigner: Missing integrity file for '{binaryPath}'.");
                return result;
            }

            result.IsValid = string.Equals(result.ComputedHash, result.ExpectedHash, StringComparison.OrdinalIgnoreCase);

            if (!result.IsValid)
            {
                result.ErrorMessage = $"Hash mismatch. Expected={result.ExpectedHash} Computed={result.ComputedHash}";
                Logger.Warn($"BinarySigner: INTEGRITY MISMATCH for '{binaryPath}'. {result.ErrorMessage}");
            }
            else
            {
                Logger.Info($"BinarySigner: SHA-256 verified OK for '{Path.GetFileName(binaryPath)}'.");
            }

            return result;
        }

        /// <summary>
        /// Reads Authenticode signature details for the specified binary using Windows platform services.
        /// </summary>
        /// <param name="filePath">Absolute path to the binary file.</param>
        /// <returns>A <see cref="BinaryVerificationResult"/> with signature metadata populated.</returns>
        public static BinaryVerificationResult ReadAuthenticodeSignature(string filePath)
        {
            var result = new BinaryVerificationResult();
            result.ComputedHash = ComputeSha256(filePath);

            try
            {
                var cert2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(filePath);
                result.HasAuthenticodeSignature = true;
                result.SignerSubject = cert2.Subject;
                result.SignerIssuer = cert2.Issuer;
                result.ExpirationDate = cert2.NotAfter.ToString("o");

                if (DateTime.UtcNow > cert2.NotAfter.ToUniversalTime())
                {
                    result.SignatureState = "Expired";
                    result.IsValid = false;
                    result.ErrorMessage = $"Certificate expired on {cert2.NotAfter:yyyy-MM-dd}.";
                }
                else
                {
                    result.SignatureState = "Signed";
                    result.IsValid = true;
                }
                Logger.Info($"BinarySigner: Authenticode signature found for '{Path.GetFileName(filePath)}'. Signer: {cert2.Subject}, State: {result.SignatureState}");
            }
            catch (Exception ex)
            {
                result.HasAuthenticodeSignature = false;
                result.SignatureState = "Unsigned";
                result.IsValid = false;
                result.ErrorMessage = $"No valid Authenticode signature: {ex.Message}";
                Logger.Warn($"BinarySigner: No Authenticode signature found for '{filePath}': {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Generates an integrity.sha256 file for a target binary.
        /// </summary>
        /// <param name="binaryPath">Absolute path to the binary to hash.</param>
        /// <param name="outputHashFilePath">Destination path for the .sha256 file.</param>
        public static void GenerateIntegrityFile(string binaryPath, string outputHashFilePath)
        {
            var hash = ComputeSha256(binaryPath);
            if (string.IsNullOrEmpty(hash))
            {
                Logger.Warn($"BinarySigner: Could not generate integrity file for '{binaryPath}': hash computation failed.");
                return;
            }

            File.WriteAllText(outputHashFilePath, hash, Encoding.UTF8);
            Logger.Info($"BinarySigner: Integrity file generated at '{outputHashFilePath}' (SHA-256={hash}).");
        }
    }
}
