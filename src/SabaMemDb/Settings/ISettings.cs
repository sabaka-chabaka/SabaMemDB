namespace SabaMemDb.Settings;

public interface ISettings
{
    public string Password { get; }
    public int MaxEntries { get; }
    public int BufferSize { get; }
}