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

app.MapPost("/api/db/{key}", async (string key, HttpRequest request, StorageEngine db) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var valueBytes = ms.ToArray(); 

    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);

    db.Set(keyBytes, valueBytes.AsSpan());

    return Results.Ok();
});

app.MapPost("/api/db/setnx/{key}", async (string key, HttpRequest request, StorageEngine db) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var valueBytes = ms.ToArray(); 

    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);

    return db.SetNotExists(keyBytes, valueBytes.AsSpan()) ? Results.Ok() : Results.Conflict();
});

app.MapGet("/api/db/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    var valueSpan = db.Get(keyBytes);

    return valueSpan.IsEmpty ? Results.NotFound() : Results.Bytes([.. valueSpan], "application/octet-stream");
});

app.MapDelete("/api/db/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Delete(keyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/db/exists/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Exists(keyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/{oldKey}/{newKey}", (string oldKey, string newKey, StorageEngine db) =>
{
    Span<byte> newKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(newKey)];
    System.Text.Encoding.UTF8.GetBytes(newKey, newKeyBytes);
    
    Span<byte> oldKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(oldKey)];
    System.Text.Encoding.UTF8.GetBytes(oldKey, oldKeyBytes);
    
    return db.Rename(oldKeyBytes, newKeyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/{oldKey}", (string oldKey, string newKey, StorageEngine db) =>
{
    Span<byte> newKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(newKey)];
    System.Text.Encoding.UTF8.GetBytes(newKey, newKeyBytes);
    
    Span<byte> oldKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(oldKey)];
    System.Text.Encoding.UTF8.GetBytes(oldKey, oldKeyBytes);
    
    return db.Rename(oldKeyBytes, newKeyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/renamenx/{oldKey}/{newKey}", (string oldKey, string newKey, StorageEngine db) =>
{
    Span<byte> newKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(newKey)];
    System.Text.Encoding.UTF8.GetBytes(newKey, newKeyBytes);
    
    Span<byte> oldKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(oldKey)];
    System.Text.Encoding.UTF8.GetBytes(oldKey, oldKeyBytes);
    
    return db.RenameNotExists(oldKeyBytes, newKeyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/renamenx/{oldKey}", (string oldKey, string newKey, StorageEngine db) =>
{
    Span<byte> newKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(newKey)];
    System.Text.Encoding.UTF8.GetBytes(newKey, newKeyBytes);
    
    Span<byte> oldKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(oldKey)];
    System.Text.Encoding.UTF8.GetBytes(oldKey, oldKeyBytes);
    
    return db.RenameNotExists(oldKeyBytes, newKeyBytes) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/expire/{key}/{seconds}", (string key, int seconds, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Expire(keyBytes, seconds) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/pexpire/{key}/{milliseconds}", (string key, int milliseconds, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.PExpire(keyBytes, milliseconds) ? Results.Ok() : Results.NotFound();
});

app.MapPatch("/api/db/expireat/{key}/{timestamp}", (string key, long timestamp, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.ExpireAt(keyBytes, timestamp) ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/db/ttl/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Ttl(keyBytes);
});

app.MapGet("/api/db/pttl/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Pttl(keyBytes);
});

app.MapPatch("/api/db/persist/{key}", (string key, StorageEngine db) =>
{
    Span<byte> keyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(key)];
    System.Text.Encoding.UTF8.GetBytes(key, keyBytes);
    
    return db.Persist(keyBytes) ? Results.Ok() : Results.NotFound();
});

app.Run();