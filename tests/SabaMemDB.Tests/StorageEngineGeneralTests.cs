using System.Text;
using SabaMemDb.Engine;
using SabaMemDb.Settings;
using Xunit;

namespace SabaMemDB.Tests;

public class StorageEngineGeneralTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Ping_ReturnsPong()
    {
        var db = new StorageEngine(100, 1024 * 1024);
        Assert.Equal("Pong!", db.Ping());
    }

    [Fact]
    public void Constructor_WithISettings_InitializesCorrectly()
    {
        var settings = new Settings("pass123", 256, 16); // 16 MB buffer
        var db = new StorageEngine(settings);

        Assert.NotNull(db);
        Assert.Equal(0, db.Count);

        db.Set(B("test"), B("val"));
        Assert.Equal("val", Encoding.UTF8.GetString(db.Get(B("test"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void Multithreaded_ConcurrentReads_MaintainConsistency()
    {
        var db = new StorageEngine(2000, 10 * 1024 * 1024);
        const int keyCount = 100;

        for (int i = 0; i < keyCount; i++)
        {
            db.Set(B($"key_{i}"), B($"val_{i}"));
        }

        Assert.Equal(keyCount, db.Count);

        Parallel.For(0, 20, _ =>
        {
            for (int i = 0; i < keyCount; i++)
            {
                var val = db.Get(B($"key_{i}"));
                Assert.False(val.IsEmpty);
                Assert.Equal($"val_{i}", Encoding.UTF8.GetString(val));
                Assert.True(db.Exists(B($"key_{i}")));
            }
        });
    }

    [Fact]
    public void MultipleSequentialOperations_MaintainStateAndCount()
    {
        var db = new StorageEngine(500, 1024 * 1024);

        for (int i = 0; i < 50; i++)
        {
            db.Set(B($"key_{i}"), B($"val_{i}"));
        }

        Assert.Equal(50, db.Count);

        for (int i = 0; i < 25; i++)
        {
            Assert.True(db.Delete(B($"key_{i}")));
        }

        Assert.Equal(25, db.Count);

        for (int i = 25; i < 50; i++)
        {
            var val = db.Get(B($"key_{i}"));
            Assert.Equal($"val_{i}", Encoding.UTF8.GetString(val));
        }
    }
}
