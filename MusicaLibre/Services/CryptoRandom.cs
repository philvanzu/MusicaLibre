namespace MusicaLibre.Services;

using System;
using System.Security.Cryptography;

public static class CryptoRandom
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

    public static int NextInt()
    {
        var bytes = new byte[4];
        _rng.GetBytes(bytes);
        
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue; // Keep it non-negative
    }

    public static Int32 NextInt(int min, int max)
    {
        return RandomNumberGenerator.GetInt32(min, max);
    }
}