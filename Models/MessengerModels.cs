namespace PROXIMAMOP.Models;

public class MessengerUserDto
{
    public int Id { get; set; }
    public int MainAppUserId { get; set; }
    public string DeviceId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsBlockedFromMessenger { get; set; }
    public bool IsPrivateMessagingEnabled { get; set; }
}

public class SyncMessengerUserRequest
{
    public int MainAppUserId { get; set; }
    public string DeviceId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}

public class StartPrivateConversationRequest
{
    public string DeviceId { get; set; } = "";
    public int TargetUserId { get; set; }
}

public class SendPrivateTextRequest
{
    public string DeviceId { get; set; } = "";
    public long ConversationId { get; set; }
    public string Text { get; set; } = "";
}

public class MarkConversationReadRequest
{
    public string DeviceId { get; set; } = "";
    public long ConversationId { get; set; }
    public long? LastReadMessageId { get; set; }
}

public class ConversationListItemDto
{
    public long ConversationId { get; set; }
    public string ConversationType { get; set; } = "";
    public int OtherUserId { get; set; }
    public int OtherMainAppUserId { get; set; }
    public string OtherUserName { get; set; } = "";
    public string OtherAvatarUrl { get; set; } = "";
    public string? LastMessageText { get; set; }
    public string? LastMessageType { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class PrivateMessageDto
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public int SenderUserId { get; set; }
    public string SenderUserName { get; set; } = "";
    public string SenderAvatarUrl { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? FileUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeletedForEveryone { get; set; }
    public DateTime? DeletedForEveryoneAtUtc { get; set; }
}