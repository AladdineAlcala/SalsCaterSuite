using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace SalsCaterSuite.Infrastructure.Identity;

public class LegacyPasswordHasher<TUser> : PasswordHasher<TUser> where TUser : class
{
    public override PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] decodedHashedPassword;
        try
        {
            decodedHashedPassword = Convert.FromBase64String(hashedPassword);
        }
        catch
        {
            return PasswordVerificationResult.Failed;
        }

        // Check if standard ASP.NET Identity v2 format (header byte 0x00)
        if (decodedHashedPassword.Length == 49 && decodedHashedPassword[0] == 0x00)
        {
            if (VerifyHashedPasswordV2(decodedHashedPassword, providedPassword))
            {
                // Indicates user verified successfully with legacy hash and should be upgraded to v3 hash upon login
                return PasswordVerificationResult.SuccessRehashNeeded;
            }

            return PasswordVerificationResult.Failed;
        }

        // Delegate to default ASP.NET Core Identity hasher for v3 hashes
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }

    private static bool VerifyHashedPasswordV2(byte[] hashedPassword, string password)
    {
        const int saltSize = 16;
        const int subkeyLength = 32;

        if (hashedPassword.Length != 1 + saltSize + subkeyLength)
        {
            return false;
        }

        byte[] salt = new byte[saltSize];
        Buffer.BlockCopy(hashedPassword, 1, salt, 0, saltSize);

        byte[] expectedSubkey = new byte[subkeyLength];
        Buffer.BlockCopy(hashedPassword, 1 + saltSize, expectedSubkey, 0, subkeyLength);

        // .NET 10 obsoletes the Rfc2898DeriveBytes constructors (SYSLIB0060). The static Pbkdf2
        // helper is the supported equivalent and produces byte-identical PBKDF2-HMAC-SHA1 output
        // for the same password, salt, iteration count and output length, so legacy hash
        // verification behaviour is unchanged.
        byte[] actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 1000,
            HashAlgorithmName.SHA1,
            subkeyLength);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}

