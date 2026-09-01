using SabaMemDb.Engine;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddSingleton<StorageEngine>();

builder.Services.AddOpenApi();

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

app.MapPatch("/api/db/{key}", (string newKey, string oldKey, StorageEngine db) =>
{
    Span<byte> newKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(newKey)];
    System.Text.Encoding.UTF8.GetBytes(newKey, newKeyBytes);
    
    Span<byte> oldKeyBytes = stackalloc byte[System.Text.Encoding.UTF8.GetByteCount(oldKey)];
    System.Text.Encoding.UTF8.GetBytes(oldKey, oldKeyBytes);
    
    return db.Rename(oldKeyBytes, newKeyBytes) ? Results.Ok() : Results.NotFound();
});

app.Run();