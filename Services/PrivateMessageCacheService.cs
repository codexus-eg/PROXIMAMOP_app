using System.Text.Json;
using PROXIMAMOP.Models;

namespace PROXIMAMOP.Services;

public class PrivateMessageCacheService
{
    private const int MaxMessagesPerConversation = 100;

    private readonly string _baseFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public PrivateMessageCacheService()
    {
        _baseFolder = Path.Combine(FileSystem.AppDataDirectory, "private-message-cache");
        Directory.CreateDirectory(_baseFolder);
    }

    public async Task<List<PrivateMessageDto>> GetCachedMessagesAsync(long conversationId)
    {
        if (conversationId <= 0)
            return new List<PrivateMessageDto>();

        var path = GetConversationFilePath(conversationId);

        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(path))
                return new List<PrivateMessageDto>();

            var json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                return new List<PrivateMessageDto>();

            var data = JsonSerializer.Deserialize<List<PrivateMessageDto>>(json, _jsonOptions);
            return data?
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList()
                ?? new List<PrivateMessageDto>();
        }
        catch
        {
            return new List<PrivateMessageDto>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveLatestMessagesAsync(long conversationId, IEnumerable<PrivateMessageDto> messages)
    {
        if (conversationId <= 0)
            return;

        var normalized = Normalize(messages)
            .TakeLast(MaxMessagesPerConversation)
            .ToList();

        await SaveInternalAsync(conversationId, normalized);
    }

    public async Task AppendOrUpdateMessagesAsync(long conversationId, IEnumerable<PrivateMessageDto> messages)
    {
        if (conversationId <= 0)
            return;

        var incoming = Normalize(messages).ToList();
        if (incoming.Count == 0)
            return;

        var existing = await GetCachedMessagesAsync(conversationId);

        var map = existing.ToDictionary(x => x.Id, x => x);

        foreach (var item in incoming)
            map[item.Id] = item;

        var merged = map.Values
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .TakeLast(MaxMessagesPerConversation)
            .ToList();

        await SaveInternalAsync(conversationId, merged);
    }

    public async Task ClearConversationAsync(long conversationId)
    {
        if (conversationId <= 0)
            return;

        var path = GetConversationFilePath(conversationId);

        await _lock.WaitAsync();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(long conversationId, List<PrivateMessageDto> messages)
    {
        var path = GetConversationFilePath(conversationId);

        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(messages, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
        }
    }

    private IEnumerable<PrivateMessageDto> Normalize(IEnumerable<PrivateMessageDto>? messages)
    {
        if (messages is null)
            return Enumerable.Empty<PrivateMessageDto>();

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
    private string GetConversationFilePath(long conversationId)
    {
        return Path.Combine(_baseFolder, $"conversation_{conversationId}.json");
    }
}