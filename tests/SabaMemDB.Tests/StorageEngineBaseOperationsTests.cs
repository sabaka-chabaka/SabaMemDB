using System.Text;
using SabaMemDb.Engine;
using Xunit;

namespace SabaMemDB.Tests;

public class StorageEngineBaseOperationsTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Set_And_Get_ReturnsStoredValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("value1"));
        var val = db.Get(B("key1"));

        Assert.Equal("value1", Encoding.UTF8.GetString(val));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void Get_NonExistentKey_ReturnsEmptySpan()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var val = db.Get(B("non_existent"));
        Assert.True(val.IsEmpty);
    }

    [Fact]
    public void Set_OverwritesExistingKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("initial"));
        Assert.Equal("initial", Encoding.UTF8.GetString(db.Get(B("key1"))));
        Assert.Equal(1, db.Count);

        db.Set(B("key1"), B("updated"));
        Assert.Equal("updated", Encoding.UTF8.GetString(db.Get(B("key1"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void Delete_ExistingKey_ReturnsTrueAndRemovesKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("value1"));
        Assert.Equal(1, db.Count);

        var deleted = db.Delete(B("key1"));
        Assert.True(deleted);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal(0, db.Count);
    }

    [Fact]
    public void Delete_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var deleted = db.Delete(B("non_existent"));
        Assert.False(deleted);
    }

    [Fact]
    public void Delete_ExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("tempKey"), B("tempValue"));
        db.PExpire(B("tempKey"), 1); // 1 millisecond

        Thread.Sleep(20);

        var deleted = db.Delete(B("tempKey"));
        Assert.False(deleted);
        Assert.Equal(0, db.Count);
    }

    [Fact]
    public void Exists_ReturnsTrueForExisting_FalseForNonExistent()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        Assert.False(db.Exists(B("key1")));
        db.Set(B("key1"), B("value1"));
        Assert.True(db.Exists(B("key1")));
    }

    [Fact]
    public void Exists_ReturnsFalseForExpiredKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("value1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        Assert.False(db.Exists(B("key1")));
    }

    [Fact]
    public void SetNotExists_WhenKeyDoesNotExist_ReturnsTrueAndSetsValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.SetNotExists(B("newKey"), B("newValue"));
        Assert.True(result);
        Assert.Equal("newValue", Encoding.UTF8.GetString(db.Get(B("newKey"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void SetNotExists_WhenKeyExists_ReturnsFalseAndDoesNotOverwrite()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("value1"));
        var result = db.SetNotExists(B("key1"), B("value2"));

        Assert.False(result);
        Assert.Equal("value1", Encoding.UTF8.GetString(db.Get(B("key1"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void SetNotExists_WhenKeyExistsButExpired_ReturnsTrueAndSetsValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("expKey"), B("oldVal"));
        db.PExpire(B("expKey"), 1);

        Thread.Sleep(20);

        var result = db.SetNotExists(B("expKey"), B("newVal"));
        Assert.True(result);
        Assert.Equal("newVal", Encoding.UTF8.GetString(db.Get(B("expKey"))));
    }

    [Fact]
    public void Rename_WhenKeyExists_RenamesSuccessfully()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("oldKey"), B("myValue"));
        var renamed = db.Rename(B("oldKey"), B("newKey"));

        Assert.True(renamed);
        Assert.True(db.Get(B("oldKey")).IsEmpty);
        Assert.Equal("myValue", Encoding.UTF8.GetString(db.Get(B("newKey"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void Rename_SameKey_ReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("sameKey"), B("val"));
        var renamed = db.Rename(B("sameKey"), B("sameKey"));

        Assert.True(renamed);
        Assert.Equal("val", Encoding.UTF8.GetString(db.Get(B("sameKey"))));
    }

    [Fact]
    public void Rename_NonExistentOldKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var renamed = db.Rename(B("non_existent"), B("newKey"));
        Assert.False(renamed);
    }

    [Fact]
    public void Rename_ExpiredOldKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("oldExp"), B("val"));
        db.PExpire(B("oldExp"), 1);

        Thread.Sleep(20);

        var renamed = db.Rename(B("oldExp"), B("newKey"));
        Assert.False(renamed);
        Assert.True(db.Get(B("newKey")).IsEmpty);
    }

    [Fact]
    public void Rename_OverwritesExistingTargetKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.Set(B("key2"), B("val2"));
        Assert.Equal(2, db.Count);

        var renamed = db.Rename(B("key1"), B("key2"));
        Assert.True(renamed);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal("val1", Encoding.UTF8.GetString(db.Get(B("key2"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void RenameNotExists_WhenTargetDoesNotExist_RenamesSuccessfully()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("oldKey"), B("val"));
        var result = db.RenameNotExists(B("oldKey"), B("newKey"));

        Assert.True(result);
        Assert.True(db.Get(B("oldKey")).IsEmpty);
        Assert.Equal("val", Encoding.UTF8.GetString(db.Get(B("newKey"))));
        Assert.Equal(1, db.Count);
    }

    [Fact]
    public void RenameNotExists_SameKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val"));
        var result = db.RenameNotExists(B("key1"), B("key1"));

        Assert.False(result);
    }

    [Fact]
    public void RenameNotExists_NonExistentOldKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.RenameNotExists(B("non_existent"), B("newKey"));
        Assert.False(result);
    }

    [Fact]
    public void RenameNotExists_ExpiredOldKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("oldKey"), B("val"));
        db.PExpire(B("oldKey"), 1);

        Thread.Sleep(20);

        var result = db.RenameNotExists(B("oldKey"), B("newKey"));
        Assert.False(result);
    }

    [Fact]
    public void RenameNotExists_TargetExistsAndActive_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.Set(B("key2"), B("val2"));

        var result = db.RenameNotExists(B("key1"), B("key2"));
        Assert.False(result);
        Assert.Equal("val1", Encoding.UTF8.GetString(db.Get(B("key1"))));
        Assert.Equal("val2", Encoding.UTF8.GetString(db.Get(B("key2"))));
    }

    [Fact]
    public void RenameNotExists_TargetExistsButExpired_ReturnsTrueAndOverwrites()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.Set(B("key2"), B("val2"));
        db.PExpire(B("key2"), 1);

        Thread.Sleep(20);

        var result = db.RenameNotExists(B("key1"), B("key2"));
        Assert.True(result);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal("val1", Encoding.UTF8.GetString(db.Get(B("key2"))));
    }

    [Fact]
    public void Set_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 10); // only 10 bytes buffer

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.Set(B("long_key_name"), B("long_value_string"));
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void SetNotExists_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 10); // only 10 bytes buffer

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.SetNotExists(B("long_key_name"), B("long_value_string"));
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void Rename_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 15);
        db.Set(B("k1"), B("v1"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.Rename(B("k1"), B("very_long_new_key_that_exceeds_buffer"));
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void RenameNotExists_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 15);
        db.Set(B("k1"), B("v1"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.RenameNotExists(B("k1"), B("very_long_new_key_that_exceeds_buffer"));
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void StorageIndexFull_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(2, 1024 * 1024);
        db.Set(B("key1"), B("v1"));
        db.Set(B("key2"), B("v2"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.Set(B("key3"), B("v3"));
        });

        Assert.Equal("Storage index is full", ex.Message);
    }

    [Fact]
    public void LinearProbing_HandlesCollisionsAndDeletions()
    {
        var db = new StorageEngine(16, 1024 * 1024);

        for (int i = 0; i < 10; i++)
        {
            db.Set(B($"item_{i}"), B($"value_{i}"));
        }

        Assert.Equal(10, db.Count);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"value_{i}", Encoding.UTF8.GetString(db.Get(B($"item_{i}"))));
        }

        // Delete middle items
        Assert.True(db.Delete(B("item_3")));
        Assert.True(db.Delete(B("item_7")));
        Assert.Equal(8, db.Count);

        // Remaining items should still be accessible through linear probing
        for (int i = 0; i < 10; i++)
        {
            if (i == 3 || i == 7)
            {
                Assert.True(db.Get(B($"item_{i}")).IsEmpty);
            }
            else
            {
                Assert.Equal($"value_{i}", Encoding.UTF8.GetString(db.Get(B($"item_{i}"))));
            }
        }
    }
}
