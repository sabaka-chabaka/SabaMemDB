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

app.Run();