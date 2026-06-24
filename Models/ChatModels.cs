namespace PROXIMAMOP.Models;

public class ChatUserDto
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Bio { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string Status { get; set; } = "";
    public string Role { get; set; } = "";
    public string? BanReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ProfileUpdatedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }

    public bool IsAnalyst { get; set; }
    public int AnalystStars { get; set; }
    public bool CanPostAnalystContent { get; set; }
    public string AnalystBio { get; set; } = "";
    public int AnalystDisplayOrder { get; set; }
}

public class ChatMessageDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string Text { get; set; } = "";
    public string Type { get; set; } = "";
    public string? FileUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? StickerCode { get; set; }
    public int? DurationSeconds { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }
}

public class ChatMessageViewModel
{
    public long Id { get; set; }
    public int UserId { get; set; }

    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Text { get; set; } = "";
    public string TimeText { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string Type { get; set; } = "";
    public string BadgeType { get; set; } = "None";

    public bool IsMine { get; set; }
    public bool IsImage { get; set; }
    public string ImageUrl { get; set; } = "";

    public bool IsVoice { get; set; }
    public string VoiceUrl { get; set; } = "";
    public string VoiceDurationText { get; set; } = "";

    public bool HasText { get; set; }
    public bool IsSystem { get; set; }

    public string BadgeIcon { get; set; } = "";
    public bool HasBadgeIcon { get; set; }
    public Color BadgeBorderColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconBackgroundColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconTextColor { get; set; } = Colors.White;

    public Color BubbleColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.White;

    public LayoutOptions MessageRowHorizontalOptions { get; set; } = LayoutOptions.Start;
    public LayoutOptions HeaderHorizontalOptions { get; set; } = LayoutOptions.Start;
    public TextAlignment MessageTextAlignment { get; set; } = TextAlignment.Start;

    public bool ShowAvatar { get; set; } = true;
    public bool ShowBadgeOnAvatar { get; set; } = false;
    public bool ShowBadgeNearName { get; set; } = false;
}

public class RegisterUserRequest
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
}

public class SendMessageRequest
{
    public string DeviceId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class UpdateProfileRequest
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Bio { get; set; }
}

public class AnalystDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Bio { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public int AnalystStars { get; set; }
    public string AnalystBio { get; set; } = "";
    public int AnalystDisplayOrder { get; set; }
    public bool CanPostAnalystContent { get; set; }
}

public class AnalystPostDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public int AnalystStars { get; set; }
    public string Text { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public int CommentsCount { get; set; }
}

public class AnalystCommentDto
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

public class AnalystPostsPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<AnalystPostDto> Items { get; set; } = new();
}

public class AnalystCommentsPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<AnalystCommentDto> Items { get; set; } = new();
}

public class AddAnalystCommentRequest
{
    public string DeviceId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class DeviceActionRequest
{
    public string DeviceId { get; set; } = "";
}

public class AnalystListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Bio { get; set; } = "";
    public string AnalystBio { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public int AnalystStars { get; set; }
    public string StarsText { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string BadgeIcon { get; set; } = "";
    public bool HasBadge { get; set; }
    public Color BadgeBorderColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconBackgroundColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconTextColor { get; set; } = Colors.White;

    public bool IsFollowing { get; set; }
    public string FollowStateText { get; set; } = "";
    public Color FollowStateBackgroundColor { get; set; } = Color.FromArgb("#1D2438");
    public Color FollowStateTextColor { get; set; } = Colors.White;

    public bool NotificationsEnabled { get; set; }
    public string NotificationStateText { get; set; } = "";
    public Color NotificationStateBackgroundColor { get; set; } = Color.FromArgb("#1D2438");
    public Color NotificationStateTextColor { get; set; } = Colors.White;
}

public class AnalystPostViewModel
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string BadgeIcon { get; set; } = "";
    public bool HasBadge { get; set; }
    public Color BadgeBorderColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconBackgroundColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconTextColor { get; set; } = Colors.White;
    public int AnalystStars { get; set; }
    public string StarsText { get; set; } = "";
    public string Text { get; set; } = "";
    public bool HasText { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool HasImage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string TimeText { get; set; } = "";
    public int CommentsCount { get; set; }
    public string CommentsText { get; set; } = "";
    public bool CanDelete { get; set; }
}

public class AnalystCommentViewModel
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string BadgeType { get; set; } = "None";
    public string BadgeIcon { get; set; } = "";
    public bool HasBadge { get; set; }
    public Color BadgeBorderColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconBackgroundColor { get; set; } = Color.FromArgb("#2E2E2E");
    public Color BadgeIconTextColor { get; set; } = Colors.White;
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string TimeText { get; set; } = "";
    public bool CanDelete { get; set; }
    public bool CanReply { get; set; } = true;
}