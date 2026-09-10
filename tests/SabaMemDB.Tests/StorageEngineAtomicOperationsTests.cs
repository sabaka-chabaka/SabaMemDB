using System.Text;
using SabaMemDb.Engine;
using Xunit;

namespace SabaMemDB.Tests;

public class StorageEngineAtomicOperationsTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Incr_IncrementsNumericValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("10"));
        var result = db.Incr(B("counter"));

        Assert.True(result);
        Assert.Equal("11", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void Incr_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.Incr(B("non_existent"));
        Assert.False(result);
    }

    [Fact]
    public void Incr_NonNumericValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("not_a_number"), B("hello"));
        var result = db.Incr(B("not_a_number"));

        Assert.False(result);
        Assert.Equal("hello", Encoding.UTF8.GetString(db.Get(B("not_a_number"))));
    }

    [Fact]
    public void Incr_ExpiredKey_ReturnsFalseAndDeletesKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("expCounter"), B("5"));
        db.PExpire(B("expCounter"), 1);

        Thread.Sleep(20);

        var result = db.Incr(B("expCounter"));
        Assert.False(result);
        Assert.True(db.Get(B("expCounter")).IsEmpty);
    }

    [Fact]
    public void Incr_AtLongMaxValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("max_val"), B(long.MaxValue.ToString()));
        var result = db.Incr(B("max_val"));

        Assert.False(result);
        Assert.Equal(long.MaxValue.ToString(), Encoding.UTF8.GetString(db.Get(B("max_val"))));
    }

    [Fact]
    public void Decr_DecrementsNumericValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("10"));
        var result = db.Decr(B("counter"));

        Assert.True(result);
        Assert.Equal("9", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void Decr_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.Decr(B("non_existent"));
        Assert.False(result);
    }

    [Fact]
    public void Decr_NonNumericValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("not_a_number"), B("world"));
        var result = db.Decr(B("not_a_number"));

        Assert.False(result);
        Assert.Equal("world", Encoding.UTF8.GetString(db.Get(B("not_a_number"))));
    }

    [Fact]
    public void Decr_ExpiredKey_ReturnsFalseAndDeletesKey()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("expCounter"), B("5"));
        db.PExpire(B("expCounter"), 1);

        Thread.Sleep(20);

        var result = db.Decr(B("expCounter"));
        Assert.False(result);
        Assert.True(db.Get(B("expCounter")).IsEmpty);
    }

    [Fact]
    public void Decr_AtLongMinValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("min_val"), B(long.MinValue.ToString()));
        var result = db.Decr(B("min_val"));

        Assert.False(result);
        Assert.Equal(long.MinValue.ToString(), Encoding.UTF8.GetString(db.Get(B("min_val"))));
    }

    [Fact]
    public void IncrBy_IncrementsByPositiveValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("100"));
        var result = db.IncrBy(B("counter"), 50);

        Assert.True(result);
        Assert.Equal("150", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void IncrBy_WithNegativeValue_Decrements()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("100"));
        var result = db.IncrBy(B("counter"), -30);

        Assert.True(result);
        Assert.Equal("70", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void IncrBy_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.IncrBy(B("non_existent"), 10);
        Assert.False(result);
    }

    [Fact]
    public void IncrBy_NonNumericValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B("abc"));
        var result = db.IncrBy(B("key"), 10);

        Assert.False(result);
    }

    [Fact]
    public void IncrBy_ExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B("10"));
        db.PExpire(B("key"), 1);

        Thread.Sleep(20);

        var result = db.IncrBy(B("key"), 10);
        Assert.False(result);
    }

    [Fact]
    public void IncrBy_OverflowProtection_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B((long.MaxValue - 10).ToString()));
        var result = db.IncrBy(B("key"), 20);

        Assert.False(result);
    }

    [Fact]
    public void IncrBy_UnderflowProtection_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B((long.MinValue + 10).ToString()));
        var result = db.IncrBy(B("key"), -20);

        Assert.False(result);
    }

    [Fact]
    public void DecrBy_DecrementsByPositiveValue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("100"));
        var result = db.DecrBy(B("counter"), 40);

        Assert.True(result);
        Assert.Equal("60", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void DecrBy_WithNegativeValue_Increments()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("counter"), B("100"));
        var result = db.DecrBy(B("counter"), -25);

        Assert.True(result);
        Assert.Equal("125", Encoding.UTF8.GetString(db.Get(B("counter"))));
    }

    [Fact]
    public void DecrBy_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.DecrBy(B("non_existent"), 10);
        Assert.False(result);
    }

    [Fact]
    public void DecrBy_NonNumericValue_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B("xyz"));
        var result = db.DecrBy(B("key"), 10);

        Assert.False(result);
    }

    [Fact]
    public void DecrBy_ExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B("10"));
        db.PExpire(B("key"), 1);

        Thread.Sleep(20);

        var result = db.DecrBy(B("key"), 10);
        Assert.False(result);
    }

    [Fact]
    public void DecrBy_UnderflowProtection_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B((long.MinValue + 10).ToString()));
        var result = db.DecrBy(B("key"), 20);

        Assert.False(result);
    }

    [Fact]
    public void DecrBy_OverflowProtection_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key"), B((long.MaxValue - 10).ToString()));
        var result = db.DecrBy(B("key"), -20);

        Assert.False(result);
    }

    [Fact]
    public void Incr_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 6);
        db.Set(B("k"), B("9")); // used 1+1=2 bytes, remaining 4 bytes

        // Incr will format "10" (2 bytes). 2+2 = 4 <= 6.
        db.Incr(B("k")); // "10"
        // Incr will format "11" (2 bytes). 4+2 = 6 <= 6.
        db.Incr(B("k")); // "11"
        // Next Incr will exceed buffer (6 + 2 > 6)
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.Incr(B("k"));
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void Decr_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 4);
        db.Set(B("k"), B("9")); // 2 bytes used, 2 bytes remaining
        db.Decr(B("k")); // 1 byte used -> 3 bytes
        db.Decr(B("k")); // 1 byte used -> 4 bytes
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.Decr(B("k")); // 5 bytes > 4
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void IncrBy_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 5);
        db.Set(B("k"), B("1"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.IncrBy(B("k"), 10000);
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }

    [Fact]
    public void DecrBy_WhenBufferCapacityExceeded_ThrowsInvalidOperationException()
    {
        var db = new StorageEngine(100, 5);
        db.Set(B("k"), B("1"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            db.DecrBy(B("k"), 10000);
        });

        Assert.Equal("Not enough space in the buffer", ex.Message);
    }
}
