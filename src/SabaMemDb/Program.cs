using System.Buffers;
using System.Buffers.Text;
using System.Text.Json.Serialization.Metadata;
using SabaMemDb.Engine;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<StorageEngine>();

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        options.SerializerOptions.TypeInfoResolver,
        new DefaultJsonTypeInfoResolver()
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/api/db/set/{key}", async (string key, HttpRequest request, HttpResponse response, StorageEngine db) =>
{
    var reader = request.BodyReader;
    while (true)
    {
        var readResult = await reader.ReadAsync();
        var buffer = readResult.Buffer;
        if (readResult.IsCompleted)
        {
            using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
            if (buffer.IsSingleSegment)
            {
                db.Set(keyBytes.Span, buffer.FirstSpan);
            }
            else
            {
                var len = (int)buffer.Length;
                var rentedBody = ArrayPool<byte>.Shared.Rent(len);
                try
                {
                    buffer.CopyTo(rentedBody);
                    db.Set(keyBytes.Span, rentedBody.AsSpan(0, len));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBody);
                }
            }
            reader.AdvanceTo(buffer.End);
            break;
        }
        reader.AdvanceTo(buffer.Start, buffer.End);
    }

    response.StatusCode = StatusCodes.Status200OK;
});

app.MapPost("/api/db/setnx/{key}", async (string key, HttpRequest request, HttpResponse response, StorageEngine db) =>
{
    var reader = request.BodyReader;
    var success = false;
    while (true)
    {
        var readResult = await reader.ReadAsync();
        var buffer = readResult.Buffer;
        if (readResult.IsCompleted)
        {
            using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
            if (buffer.IsSingleSegment)
            {
                success = db.SetNotExists(keyBytes.Span, buffer.FirstSpan);
            }
            else
            {
                var len = (int)buffer.Length;
                var rentedBody = ArrayPool<byte>.Shared.Rent(len);
                try
                {
                    buffer.CopyTo(rentedBody);
                    success = db.SetNotExists(keyBytes.Span, rentedBody.AsSpan(0, len));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBody);
                }
            }
            reader.AdvanceTo(buffer.End);
            break;
        }
        reader.AdvanceTo(buffer.Start, buffer.End);
    }

    response.StatusCode = success ? StatusCodes.Status200OK : StatusCodes.Status409Conflict;
});

app.MapGet("/api/db/get/{key}", async (string key, StorageEngine db, HttpResponse response) =>
{
    bool found;
    {
        using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
        var valueSpan = db.Get(keyBytes.Span);
        found = !valueSpan.IsEmpty;
        if (found)
        {
            response.ContentType = "application/octet-stream";
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentLength = valueSpan.Length;
            response.BodyWriter.Write(valueSpan);
        }
    }

    if (!found)
    {
        response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await response.BodyWriter.FlushAsync();
});

app.MapDelete("/api/db/delete/{key}", (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Delete(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapGet("/api/db/exists/{key}", (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Exists(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/rename/{oldKey}/{newKey}", (string oldKey, string newKey, StorageEngine db, HttpResponse response) =>
{
    using var oldKeyBytes = new RentedOrStackKey(oldKey, stackalloc byte[512]);
    using var newKeyBytes = new RentedOrStackKey(newKey, stackalloc byte[512]);
    
    response.StatusCode = db.Rename(oldKeyBytes.Span, newKeyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/renamenx/{oldKey}/{newKey}", (string oldKey, string newKey, StorageEngine db, HttpResponse response) =>
{
    using var oldKeyBytes = new RentedOrStackKey(oldKey, stackalloc byte[512]);
    using var newKeyBytes = new RentedOrStackKey(newKey, stackalloc byte[512]);
    
    response.StatusCode = db.RenameNotExists(oldKeyBytes.Span, newKeyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/expire/{key}/{seconds:int}", (string key, int seconds, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Expire(keyBytes.Span, seconds) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/pexpire/{key}/{milliseconds:int}", (string key, int milliseconds, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.PExpire(keyBytes.Span, milliseconds) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/expireat/{key}/{timestamp:long}", (string key, long timestamp, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.ExpireAt(keyBytes.Span, timestamp) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapGet("/api/db/ttl/{key}", async (string key, StorageEngine db, HttpResponse response) =>
{
    int ttl;
    {
        using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
        ttl = db.Ttl(keyBytes.Span);
    }

    Span<byte> formatted = stackalloc byte[32];
    Utf8Formatter.TryFormat(ttl, formatted, out int written);

    response.ContentType = "application/json; charset=utf-8";
    response.StatusCode = StatusCodes.Status200OK;
    response.ContentLength = written;
    response.BodyWriter.Write(formatted[..written]);
    await response.BodyWriter.FlushAsync();
});

app.MapGet("/api/db/pttl/{key}", async (string key, StorageEngine db, HttpResponse response) =>
{
    int pttl;
    {
        using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
        pttl = db.Pttl(keyBytes.Span);
    }

    Span<byte> formatted = stackalloc byte[32];
    Utf8Formatter.TryFormat(pttl, formatted, out int written);

    response.ContentType = "application/json; charset=utf-8";
    response.StatusCode = StatusCodes.Status200OK;
    response.ContentLength = written;
    response.BodyWriter.Write(formatted[..written]);
    await response.BodyWriter.FlushAsync();
});

app.MapPatch("/api/db/persist/{key}", (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Persist(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.Run();

ref struct RentedOrStackKey : IDisposable
{
    private byte[]? _rented;
    public readonly Span<byte> Span;

    public RentedOrStackKey(string key, Span<byte> stackBuffer)
    {
        int count = System.Text.Encoding.UTF8.GetByteCount(key);
        if (count <= stackBuffer.Length)
        {
            _rented = null;
            Span = stackBuffer[..count];
        }
        else
        {
            _rented = ArrayPool<byte>.Shared.Rent(count);
            Span = _rented.AsSpan(0, count);
        }

        System.Text.Encoding.UTF8.GetBytes(key, Span);
    }

    public void Dispose()
    {
        if (_rented != null)
        {
            ArrayPool<byte>.Shared.Return(_rented);
            _rented = null;
        }
    }
}