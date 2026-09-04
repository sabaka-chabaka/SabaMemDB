using System.Buffers;
using System.Buffers.Text;
using System.Text.Json.Serialization.Metadata;
using SabaMemDb.Engine;
using SabaMemDb.Middleware;
using SabaMemDb.Settings;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<StorageEngine>();

builder.Services.AddSingleton<ISettings>(provider =>
{
    return provider.GetRequiredService<IConfiguration>().GetSection("DbSettings").Get<Settings>();
});

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        options.SerializerOptions.TypeInfoResolver,
        new DefaultJsonTypeInfoResolver()
    );
});

var app = builder.Build();

app.UseMiddleware<AuthMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPost("/api/db/set/{key}", static async ValueTask (string key, HttpRequest request, HttpResponse response, StorageEngine db) =>
{
    var reader = request.BodyReader;
    if (reader.TryRead(out var readResult) && readResult.IsCompleted)
    {
        ExecuteSet(readResult.Buffer, key, db);
        reader.AdvanceTo(readResult.Buffer.End);
        response.StatusCode = StatusCodes.Status200OK;
        return;
    }

    await ReadAndSetAsync(key, reader, response, db);
});

app.MapPost("/api/db/setnx/{key}", static async ValueTask (string key, HttpRequest request, HttpResponse response, StorageEngine db) =>
{
    var reader = request.BodyReader;
    if (reader.TryRead(out var readResult) && readResult.IsCompleted)
    {
        var success = ExecuteSetNx(readResult.Buffer, key, db);
        reader.AdvanceTo(readResult.Buffer.End);
        response.StatusCode = success ? StatusCodes.Status200OK : StatusCodes.Status409Conflict;
        return;
    }

    await ReadAndSetNxAsync(key, reader, response, db);
});

app.MapGet("/api/db/get/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    var valueSpan = db.Get(keyBytes.Span);
    if (valueSpan.IsEmpty)
    {
        response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    response.ContentType = "application/octet-stream";
    response.StatusCode = StatusCodes.Status200OK;
    response.ContentLength = valueSpan.Length;
    response.BodyWriter.Write(valueSpan);
});

app.MapDelete("/api/db/delete/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Delete(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapGet("/api/db/exists/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Exists(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/rename/{oldKey}/{newKey}", static (string oldKey, string newKey, StorageEngine db, HttpResponse response) =>
{
    using var oldKeyBytes = new RentedOrStackKey(oldKey, stackalloc byte[512]);
    using var newKeyBytes = new RentedOrStackKey(newKey, stackalloc byte[512]);
    
    response.StatusCode = db.Rename(oldKeyBytes.Span, newKeyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/renamenx/{oldKey}/{newKey}", static (string oldKey, string newKey, StorageEngine db, HttpResponse response) =>
{
    using var oldKeyBytes = new RentedOrStackKey(oldKey, stackalloc byte[512]);
    using var newKeyBytes = new RentedOrStackKey(newKey, stackalloc byte[512]);
    
    response.StatusCode = db.RenameNotExists(oldKeyBytes.Span, newKeyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/expire/{key}/{seconds:int}", static (string key, int seconds, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Expire(keyBytes.Span, seconds) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/pexpire/{key}/{milliseconds:int}", static (string key, int milliseconds, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.PExpire(keyBytes.Span, milliseconds) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/expireat/{key}/{timestamp:long}", static (string key, long timestamp, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.ExpireAt(keyBytes.Span, timestamp) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapGet("/api/db/ttl/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    int ttl;
    using (var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]))
    {
        ttl = db.Ttl(keyBytes.Span);
    }

    Span<byte> formatted = stackalloc byte[32];
    Utf8Formatter.TryFormat(ttl, formatted, out int written);

    response.ContentType = "application/json; charset=utf-8";
    response.StatusCode = StatusCodes.Status200OK;
    response.ContentLength = written;
    response.BodyWriter.Write(formatted[..written]);
});

app.MapGet("/api/db/pttl/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    int pttl;
    using (var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]))
    {
        pttl = db.Pttl(keyBytes.Span);
    }

    Span<byte> formatted = stackalloc byte[32];
    Utf8Formatter.TryFormat(pttl, formatted, out int written);

    response.ContentType = "application/json; charset=utf-8";
    response.StatusCode = StatusCodes.Status200OK;
    response.ContentLength = written;
    response.BodyWriter.Write(formatted[..written]);
});

app.MapPatch("/api/db/persist/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Persist(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/incr/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Incr(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/decr/{key}", static (string key, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.Decr(keyBytes.Span) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/incrby/{key}/{value}", static (string key, long value, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.IncrBy(keyBytes.Span, value) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.MapPatch("/api/db/decrby/{key}/{value}", static (string key, long value, StorageEngine db, HttpResponse response) =>
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    response.StatusCode = db.DecrBy(keyBytes.Span, value) ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
});

app.Run();

static void ExecuteSet(ReadOnlySequence<byte> buffer, string key, StorageEngine db)
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
}

static bool ExecuteSetNx(ReadOnlySequence<byte> buffer, string key, StorageEngine db)
{
    using var keyBytes = new RentedOrStackKey(key, stackalloc byte[512]);
    if (buffer.IsSingleSegment)
    {
        return db.SetNotExists(keyBytes.Span, buffer.FirstSpan);
    }
    else
    {
        var len = (int)buffer.Length;
        var rentedBody = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            buffer.CopyTo(rentedBody);
            return db.SetNotExists(keyBytes.Span, rentedBody.AsSpan(0, len));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBody);
        }
    }
}

static async ValueTask ReadAndSetAsync(string key, System.IO.Pipelines.PipeReader reader, HttpResponse response, StorageEngine db)
{
    while (true)
    {
        var readResult = await reader.ReadAsync();
        var buffer = readResult.Buffer;
        if (readResult.IsCompleted)
        {
            ExecuteSet(buffer, key, db);
            reader.AdvanceTo(buffer.End);
            break;
        }
        reader.AdvanceTo(buffer.Start, buffer.End);
    }
    response.StatusCode = StatusCodes.Status200OK;
}

static async ValueTask ReadAndSetNxAsync(string key, System.IO.Pipelines.PipeReader reader, HttpResponse response, StorageEngine db)
{
    var success = false;
    while (true)
    {
        var readResult = await reader.ReadAsync();
        var buffer = readResult.Buffer;
        if (readResult.IsCompleted)
        {
            success = ExecuteSetNx(buffer, key, db);
            reader.AdvanceTo(buffer.End);
            break;
        }
        reader.AdvanceTo(buffer.Start, buffer.End);
    }
    response.StatusCode = success ? StatusCodes.Status200OK : StatusCodes.Status409Conflict;
}

ref struct RentedOrStackKey : IDisposable
{
    private byte[]? _rented;
    public readonly Span<byte> Span;

    public RentedOrStackKey(string key, Span<byte> stackBuffer)
    {
        int maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(key.Length);
        if (maxBytes <= stackBuffer.Length)
        {
            int written = System.Text.Encoding.UTF8.GetBytes(key, stackBuffer);
            Span = stackBuffer[..written];
            _rented = null;
        }
        else
        {
            int count = System.Text.Encoding.UTF8.GetByteCount(key);
            if (count <= stackBuffer.Length)
            {
                int written = System.Text.Encoding.UTF8.GetBytes(key, stackBuffer);
                Span = stackBuffer[..written];
                _rented = null;
            }
            else
            {
                _rented = ArrayPool<byte>.Shared.Rent(count);
                int written = System.Text.Encoding.UTF8.GetBytes(key, _rented);
                Span = _rented.AsSpan(0, written);
            }
        }
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