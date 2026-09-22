namespace KosManager.Domain;

public static class Roles
{
    public const string Owner = "owner";
    public const string Penghuni = "penghuni";
}

public static class BillStatuses
{
    public const string Unpaid = "unpaid";
    public const string Pending = "pending";
    public const string Paid = "paid";
}

public static class RoomStatuses
{
    public const string Kosong = "kosong";
    public const string Isi = "isi";
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Penghuni;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Room
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Type { get; set; } = "standar";
    public decimal MonthlyPrice { get; set; }
    public string Status { get; set; } = RoomStatuses.Kosong;
}

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? TelegramChatId { get; set; }
    public int? UserId { get; set; }
    public int? RoomId { get; set; }
    public DateOnly MoveInDate { get; set; }
    public User? User { get; set; }
    public Room? Room { get; set; }
}

public class Bill
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Period { get; set; } = ""; // YYYY-MM
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = BillStatuses.Unpaid;
    public int RemindedStage { get; set; } // 0=none 1=H-3 2=H-1 3=H+1
    public Tenant? Tenant { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public string Method { get; set; } = "tunai"; // tunai | transfer
    public string? ProofPath { get; set; }
    public bool Verified { get; set; }
    public int? VerifiedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Bill? Bill { get; set; }
}

public class NotificationLog
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public string Channel { get; set; } = "telegram"; // telegram | fonnte | mock
    public string Stage { get; set; } = ""; // H-3 | H-1 | H+1 | test
    public string Status { get; set; } = "sent"; // sent | failed | skipped
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
