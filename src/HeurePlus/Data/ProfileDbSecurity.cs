using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace HeurePlus.Data;

/// <summary>
/// Bascule d'une base de profil en clair vers une base chiffrée SQLCipher, et
/// détection de l'état. Une base SQLite en clair commence par l'entête ASCII
/// « SQLite format 3\0 » ; une base SQLCipher a un entête chiffré, illisible.
/// </summary>
public static class ProfileDbSecurity
{
    private static readonly byte[] PlaintextMagic = Encoding.ASCII.GetBytes("SQLite format 3\0");

    /// <summary>Vrai si le fichier existe et est une base SQLite NON chiffrée.</summary>
    public static bool IsPlaintextDatabase(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var stream = File.OpenRead(path);
            Span<byte> head = stackalloc byte[16];
            if (stream.Read(head) < 16) return false;
            return head.SequenceEqual(PlaintextMagic);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Chiffre sur place une base en clair : export SQLCipher vers un fichier
    /// temporaire chiffré, puis remplacement de l'original. À n'appeler que si
    /// <see cref="IsPlaintextDatabase"/> est vrai.
    /// </summary>
    public static void EncryptInPlace(string path, byte[] key)
    {
        string keyHex = Convert.ToHexString(key);
        string tmp = path + ".enc";
        if (File.Exists(tmp)) File.Delete(tmp);

        var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using (var connection = new SqliteConnection(cs))
        {
            connection.Open();

            using (var attach = connection.CreateCommand())
            {
                // La clé brute doit être un littéral SQL x'…' (un paramètre lié
                // serait traité comme une passphrase et repasserait par la KDF).
                // keyHex vient de Convert.ToHexString : uniquement [0-9A-F], sûr.
                attach.CommandText = $"ATTACH DATABASE $p AS enc KEY \"x'{keyHex}'\";";
                attach.Parameters.AddWithValue("$p", tmp);
                attach.ExecuteNonQuery();
            }

            using (var export = connection.CreateCommand())
            {
                export.CommandText = "SELECT sqlcipher_export('enc');";
                export.ExecuteScalar();
            }

            using (var detach = connection.CreateCommand())
            {
                detach.CommandText = "DETACH DATABASE enc;";
                detach.ExecuteNonQuery();
            }
        }

        SqliteConnection.ClearAllPools();
        File.Delete(path);
        File.Move(tmp, path);
    }
}
