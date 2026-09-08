using System;
using System.Security.Cryptography;

namespace HeurePlus.Services;

/// <summary>
/// Hachage des mots de passe de profil : PBKDF2-SHA256, 100 000 itérations,
/// sel aléatoire de 16 octets. Le hash et le sel sont stockés en Base64 dans
/// <c>profiles.json</c> — le mot de passe en clair n'est jamais écrit.
/// </summary>
public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;

    /// <summary>Retourne le hash et le sel, tous deux encodés en Base64.</summary>
    public static (string Hash, string Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>Compare un mot de passe saisi au hash/sel stockés (temps constant).</summary>
    public static bool Verify(string password, string? hash, string? salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt)) return false;
        try
        {
            byte[] expected = Convert.FromBase64String(hash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, Convert.FromBase64String(salt), Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }
}
