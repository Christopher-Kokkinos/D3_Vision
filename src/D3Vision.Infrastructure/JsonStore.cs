using System.Text.Json;
using D3Vision.Core;

namespace D3Vision.Infrastructure;

public sealed class JsonStore : IJsonStore
{
    readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D3Vision"
    );
    readonly SemaphoreSlim gate = new(1, 1);
    readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public async Task<T> LoadAsync<T>(string name, Func<T> fallback)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = Path.Combine(directory, name + ".json");
            if (!File.Exists(path))
                return fallback();
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, options).ConfigureAwait(false)
                ?? fallback();
        }
        catch (JsonException)
        {
            return fallback();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync<T>(string name, T value)
    {
        // Snapshot caller-owned state before yielding; file I/O must not hold the gate on the UI context.
        string json = JsonSerializer.Serialize(value, options);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name + ".json");
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json).ConfigureAwait(false);
            File.Move(temp, path, true);
        }
        finally
        {
            gate.Release();
        }
    }
}
