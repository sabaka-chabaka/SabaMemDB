namespace SabaMemDb.Engine;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct Entry
{
    public ulong KeyHash;
    
    public int KeyOffset;
    public int KeyLength;
    
    public int ValueOffset;
    public int ValueLength;
    
    public long ExpiresAt;
}