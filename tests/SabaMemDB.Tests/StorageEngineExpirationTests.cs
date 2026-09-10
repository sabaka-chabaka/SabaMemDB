using System.Text;
using SabaMemDb.Engine;
using Xunit;

namespace SabaMemDB.Tests;

public class StorageEngineExpirationTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Expire_WithPositiveSeconds_SetsExpirationAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        var result = db.Expire(B("key1"), 10);

        Assert.True(result);
        var ttl = db.Ttl(B("key1"));
        Assert.InRange(ttl, 1, 10);
    }

    [Fact]
    public void Expire_WithZeroOrNegativeSeconds_DeletesEntryAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        var resultZero = db.Expire(B("key1"), 0);

        Assert.True(resultZero);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal(0, db.Count);

        db.Set(B("key2"), B("val2"));
        var resultNeg = db.Expire(B("key2"), -5);

        Assert.True(resultNeg);
        Assert.True(db.Get(B("key2")).IsEmpty);
        Assert.Equal(0, db.Count);
    }

    [Fact]
    public void Expire_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.Expire(B("non_existent"), 10);
        Assert.False(result);
    }

    [Fact]
    public void Expire_AlreadyExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        var result = db.Expire(B("key1"), 10);
        Assert.False(result);
    }

    [Fact]
    public void PExpire_WithPositiveMilliseconds_SetsExpirationAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        var result = db.PExpire(B("key1"), 5000);

        Assert.True(result);
        var pttl = db.Pttl(B("key1"));
        Assert.InRange(pttl, 1, 5000);
    }

    [Fact]
    public void PExpire_WithZeroOrNegativeMilliseconds_DeletesEntryAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        var resultZero = db.PExpire(B("key1"), 0);

        Assert.True(resultZero);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal(0, db.Count);

        db.Set(B("key2"), B("val2"));
        var resultNeg = db.PExpire(B("key2"), -100);

        Assert.True(resultNeg);
        Assert.True(db.Get(B("key2")).IsEmpty);
        Assert.Equal(0, db.Count);
    }

    [Fact]
    public void PExpire_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var result = db.PExpire(B("non_existent"), 1000);
        Assert.False(result);
    }

    [Fact]
    public void PExpire_AlreadyExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        var result = db.PExpire(B("key1"), 1000);
        Assert.False(result);
    }

    [Fact]
    public void ExpireAt_WithUnixSecondsTimestamp_SetsExpiration()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        long futureSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        var result = db.ExpireAt(B("key1"), futureSec);

        Assert.True(result);
        var ttl = db.Ttl(B("key1"));
        Assert.InRange(ttl, 1, 10);
    }

    [Fact]
    public void ExpireAt_WithUnixMillisecondsTimestamp_SetsExpiration()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        long futureMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000;
        var result = db.ExpireAt(B("key1"), futureMs);

        Assert.True(result);
        var pttl = db.Pttl(B("key1"));
        Assert.InRange(pttl, 1, 5000);
    }

    [Fact]
    public void ExpireAt_PastTimestamp_DeletesEntryAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        long pastSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10;
        var result = db.ExpireAt(B("key1"), pastSec);

        Assert.True(result);
        Assert.True(db.Get(B("key1")).IsEmpty);
        Assert.Equal(0, db.Count);
    }

    [Fact]
    public void ExpireAt_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        long futureSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        var result = db.ExpireAt(B("non_existent"), futureSec);

        Assert.False(result);
    }

    [Fact]
    public void ExpireAt_AlreadyExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        long futureSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        var result = db.ExpireAt(B("key1"), futureSec);

        Assert.False(result);
    }

    [Fact]
    public void Ttl_NonExistentKey_ReturnsMinusTwo()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        Assert.Equal(-2, db.Ttl(B("non_existent")));
    }

    [Fact]
    public void Ttl_KeyWithoutExpiration_ReturnsMinusOne()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        Assert.Equal(-1, db.Ttl(B("key1")));
    }

    [Fact]
    public void Ttl_ExpiredKey_ReturnsMinusTwo()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        Assert.Equal(-2, db.Ttl(B("key1")));
    }

    [Fact]
    public void Pttl_NonExistentKey_ReturnsMinusTwo()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        Assert.Equal(-2, db.Pttl(B("non_existent")));
    }

    [Fact]
    public void Pttl_KeyWithoutExpiration_ReturnsMinusOne()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        Assert.Equal(-1, db.Pttl(B("key1")));
    }

    [Fact]
    public void Pttl_ExpiredKey_ReturnsMinusTwo()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        Assert.Equal(-2, db.Pttl(B("key1")));
    }

    [Fact]
    public void Persist_KeyWithExpiration_RemovesExpirationAndReturnsTrue()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.Expire(B("key1"), 60);
        Assert.True(db.Ttl(B("key1")) > 0);

        var persisted = db.Persist(B("key1"));
        Assert.True(persisted);
        Assert.Equal(-1, db.Ttl(B("key1")));
        Assert.Equal(-1, db.Pttl(B("key1")));
    }

    [Fact]
    public void Persist_KeyWithoutExpiration_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        var persisted = db.Persist(B("key1"));

        Assert.False(persisted);
    }

    [Fact]
    public void Persist_NonExistentKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        var persisted = db.Persist(B("non_existent"));
        Assert.False(persisted);
    }

    [Fact]
    public void Persist_ExpiredKey_ReturnsFalse()
    {
        var db = new StorageEngine(100, 1024 * 1024);

        db.Set(B("key1"), B("val1"));
        db.PExpire(B("key1"), 1);

        Thread.Sleep(20);

        var persisted = db.Persist(B("key1"));
        Assert.False(persisted);
    }
}
