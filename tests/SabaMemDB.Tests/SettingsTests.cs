using SabaMemDb.Settings;
using Xunit;

namespace SabaMemDB.Tests;

public class SettingsTests
{
    [Fact]
    public void Settings_InitializesPropertiesCorrectly()
    {
        var settings = new Settings("mySecretPassword", 50000, 128);

        Assert.Equal("mySecretPassword", settings.Password);
        Assert.Equal(50000, settings.MaxEntries);
        Assert.Equal(128, settings.BufferSize);
    }
}
