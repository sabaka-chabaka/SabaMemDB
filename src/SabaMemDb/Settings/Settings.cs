namespace SabaMemDb.Settings;

public class Settings(string password, int maxEntries, int bufferSize) : ISettings
{
    public string Password { get; } = password;
    public int MaxEntries { get; } = maxEntries;
    public int BufferSize { get; } = bufferSize;
}