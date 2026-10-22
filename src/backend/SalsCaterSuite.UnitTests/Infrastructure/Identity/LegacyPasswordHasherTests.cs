namespace SalsCaterSuite.UnitTests.Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using SalsCaterSuite.Infrastructure.Identity;
using Xunit;

/// <summary>
/// Proves DEC-028: the SYSLIB0060 rewrite in <see cref="LegacyPasswordHasher{TUser}"/>
/// (obsolete <c>Rfc2898DeriveBytes</c> constructor → <c>Rfc2898DeriveBytes.Pbkdf2</c>)
/// is byte-identical to the legacy ASP.NET Identity <b>v2</b> format, so legacy
/// credentials keep verifying.
///
/// The expected subkey is derived from two oracles that are independent of the code
/// under test, so the test cannot pass merely by repeating the same PBKDF2 call:
/// <list type="bullet">
///   <item><b>Oracle A</b> — a hard-coded base64 v2 hash produced outside .NET. Verified
///   twice: <c>node -e "const c=require('crypto');const s=Buffer.from('SalsLegacySalt16');
///   console.log(Buffer.concat([Buffer.from([0]),s,c.pbkdf2Sync('Catering@2026',s,1000,32,'sha1')]).toString('base64'))"</c>
///   and <c>python -c "import hashlib,base64;s=b'SalsLegacySalt16';
///   print(base64.b64encode(bytes([0])+s+hashlib.pbkdf2_hmac('sha1',b'Catering@2026',s,1000,32)).decode())"</c>.
///   Both produced <c>AFNhbHNMZWdhY3lTYWx0MTZNAQGsw7/to3dDJaV0YbnNo2YJvp9tnNBF2vyusWxBfA==</c>
///   with subkey <c>4d0101acc3bfeda3774325a57461b9cda36609be9f6d9cd045dafcaeb16c417c</c>.</item>
///   <item><b>Oracle B</b> — a hand-written PBKDF2-HMAC-SHA1 block/iteration loop
///   (<see cref="ManualPbkdf2HmacSha1"/>) used to construct a second v2 hash at runtime.</item>
/// </list>
/// The salt is a compile-time constant so the vector is stable.
/// </summary>
public class LegacyPasswordHasherTests
{
    private const string Password = "Catering@2026";
    private static readonly byte[] FixedSalt = Encoding.UTF8.GetBytes("SalsLegacySalt16"); // exactly 16 bytes
    private const string SaltHex = "53616c734c656761637953616c743136";

    /// <summary>Oracle A: independently generated v2 hash (node + python agreed, see class comment).</summary>
    private const string OracleAV2Hash = "AFNhbHNMZWdhY3lTYWx0MTZNAQGsw7/to3dDJaV0YbnNo2YJvp9tnNBF2vyusWxBfA==";
    private const string OracleASubkeyHex = "4d0101acc3bfeda3774325a57461b9cda36609be9f6d9cd045dafcaeb16c417c";

    private static LegacyPasswordHasher<object> CreateHasher() => new();

    private static string Base64V2Hash(byte[] subkey)
    {
        var bytes = new byte[1 + FixedSalt.Length + subkey.Length];
        bytes[0] = 0x00; // v2 header
        Buffer.BlockCopy(FixedSalt, 0, bytes, 1, FixedSalt.Length);
        Buffer.BlockCopy(subkey, 0, bytes, 1 + FixedSalt.Length, subkey.Length);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Differential oracle: a from-scratch PBKDF2-HMAC-SHA1 (RFC 2898) implementation
    /// that shares no code with the production hasher.
    /// </summary>
    private static byte[] ManualPbkdf2HmacSha1(byte[] password, byte[] salt, int iterations, int outputLength)
    {
        const int hashLength = 20; // SHA-1
        int blockCount = (int)Math.Ceiling(outputLength / (double)hashLength);
        var output = new byte[blockCount * hashLength];

        using var hmac = new HMACSHA1(password);
        for (int block = 1; block <= blockCount; block++)
        {
            var index = new byte[]
            {
                (byte)(block >> 24), (byte)(block >> 16), (byte)(block >> 8), (byte)block
            };

            var seed = new byte[salt.Length + index.Length];
            Buffer.BlockCopy(salt, 0, seed, 0, salt.Length);
            Buffer.BlockCopy(index, 0, seed, salt.Length, index.Length);

            var u = hmac.ComputeHash(seed);
            var accumulator = (byte[])u.Clone();

            for (int i = 1; i < iterations; i++)
            {
                u = hmac.ComputeHash(u);
                for (int j = 0; j < hashLength; j++)
                {
                    accumulator[j] ^= u[j];
                }
            }

            Buffer.BlockCopy(accumulator, 0, output, (block - 1) * hashLength, hashLength);
        }

        var result = new byte[outputLength];
        Buffer.BlockCopy(output, 0, result, 0, outputLength);
        return result;
    }

    [Fact]
    public void FixedSalt_IsSixteenBytesAsConstant()
    {
        FixedSalt.Should().HaveCount(16);
        Convert.ToHexString(FixedSalt).ToLowerInvariant().Should().Be(SaltHex);
    }

    [Fact]
    public void VerifyHashedPassword_WithIndependentOracleVector_ReturnsSuccessRehashNeeded()
    {
        var result = CreateHasher().VerifyHashedPassword(new object(), OracleAV2Hash, Password);

        result.Should().Be(PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public void ManualPbkdf2Oracle_MatchesIndependentOracleVector()
    {
        var manualSubkey = ManualPbkdf2HmacSha1(Encoding.UTF8.GetBytes(Password), FixedSalt, 1000, 32);

        Convert.ToHexString(manualSubkey).ToLowerInvariant().Should().Be(OracleASubkeyHex);
    }

    [Fact]
    public void VerifyHashedPassword_WithHashBuiltByManualOracle_ReturnsSuccessRehashNeeded()
    {
        var subkey = ManualPbkdf2HmacSha1(Encoding.UTF8.GetBytes(Password), FixedSalt, 1000, 32);
        var hash = Base64V2Hash(subkey);

        var result = CreateHasher().VerifyHashedPassword(new object(), hash, Password);

        result.Should().Be(PasswordVerificationResult.SuccessRehashNeeded);
    }

    [Fact]
    public void VerifyHashedPassword_WithWrongPassword_ReturnsFailed()
    {
        var result = CreateHasher().VerifyHashedPassword(new object(), OracleAV2Hash, "WrongPassword!");

        result.Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_WithNonBase64Hash_ReturnsFailed()
    {
        var result = CreateHasher().VerifyHashedPassword(new object(), "not*base64*content", Password);

        result.Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_WithEmptyHash_ReturnsFailed()
    {
        var result = CreateHasher().VerifyHashedPassword(new object(), string.Empty, Password);

        result.Should().Be(PasswordVerificationResult.Failed);
    }

    [Fact]
    public void VerifyHashedPassword_WithNonV2Header_DelegatesToBaseHasher()
    {
        // A real v3 hash (header byte 0x01) must be handled by the base PasswordHasher.
        var v3Hasher = new PasswordHasher<object>();
        var v3Hash = v3Hasher.HashPassword(new object(), Password);

        Convert.FromBase64String(v3Hash)[0].Should().NotBe(0x00, "a v3 hash must not be mistaken for v2");

        var result = CreateHasher().VerifyHashedPassword(new object(), v3Hash, Password);

        result.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public void VerifyHashedPassword_WithNonV2HeaderAndWrongPassword_ReturnsFailed()
    {
        var v3Hasher = new PasswordHasher<object>();
        var v3Hash = v3Hasher.HashPassword(new object(), Password);

        var result = CreateHasher().VerifyHashedPassword(new object(), v3Hash, "WrongPassword!");

        result.Should().Be(PasswordVerificationResult.Failed);
    }
}
