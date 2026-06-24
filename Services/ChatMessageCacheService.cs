using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class ChatMessageCacheService
{
    private const int MaxMessages = 100;

    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public ChatMessageCacheService()
    {
        var folder = Path.Combine(FileSystem.AppDataDirectory, "chat-cache");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "general_chat_messages.json");
    }

    public async Task<List<ChatMessageDto>> GetCachedMessagesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return new List<ChatMessageDto>();

            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<ChatMessageDto>();

            var data = JsonSerializer.Deserialize<List<ChatMessageDto>>(json, _jsonOptions);
            return Normalize(data).ToList();
        }
        catch
        {
            return new List<ChatMessageDto>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveLatestMessagesAsync(IEnumerable<ChatMessageDto> messages)
    {
        var normalized = Normalize(messages)
            .TakeLast(MaxMessages)
            .ToList();

        await SaveInternalAsync(normalized);
    }

    public async Task AppendOrUpdateMessagesAsync(IEnumerable<ChatMessageDto> messages)
    {
        var incoming = Normalize(messages).ToList();
        if (incoming.Count == 0)
            return;

        var existing = await GetCachedMessagesAsync();
        var map = existing.ToDictionary(x => x.Id, x => x);

        foreach (var item in incoming)
            map[item.Id] = item;

        var merged = map.Values
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .TakeLast(MaxMessages)
            .ToList();

        await SaveInternalAsync(merged);
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(List<ChatMessageDto> messages)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(messages, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
        }
    }

    private static IEnumerable<ChatMessageDto> Normalize(IEnumerable<ChatMessageDto>? messages)
    {
        if (messages is null)
            return Enumerable.Empty<ChatMessageDto>();

        return messages
            .Where(x => x is not null && x.Id > 0)
            .GroupBy(x => x.Id)
            .Select(g => g
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Last())
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id);
    }
}