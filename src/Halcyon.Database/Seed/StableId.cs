using System.Security.Cryptography;
using System.Text;

namespace Halcyon.Database.Seed;

/// <summary>
/// A name-based id (UUID v5 layout, SHA-1): the same key always gives the same id, so the seed can tell what is already
/// in the database without a lookup table (ADR-0006).
/// </summary>
public static class StableId
{
    private static readonly byte[] Namespace = Guid.Parse("8b1b4b7e-6a43-4c55-9f0e-5c3f2a1d9e10").ToByteArray(true);

    public static Guid For(string kind, string key)
    {
        var name = Encoding.UTF8.GetBytes($"{kind}:{key.Trim().ToLowerInvariant()}");
        var input = new byte[Namespace.Length + name.Length];
        Namespace.CopyTo(input, 0);
        name.CopyTo(input, Namespace.Length);

#pragma warning disable CA5350 // SHA-1 is what UUID v5 is defined with; this is an identifier, not a security hash.
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, true);
    }

    public static Guid Agent(string email) => For("agent", email);

    public static Guid Person(string email) => For("person", email);

    public static Guid Ticket(string key) => For("ticket", key);
}
