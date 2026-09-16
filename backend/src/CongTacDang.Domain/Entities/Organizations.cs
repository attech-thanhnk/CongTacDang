using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

public class PartyCell
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // Mã chi bộ (ví dụ: CB-KT)
    public string Name { get; set; } = string.Empty; // Tên chi bộ
    public string Description { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; } // Bí thư chi bộ
    public Guid? DeputySecretaryId { get; set; } // Phó Bí thư chi bộ
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}

public class AdministrativeDepartment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // Mã phòng/xưởng
    public string Name { get; set; } = string.Empty; // Tên phòng ban / xưởng
    public string Description { get; set; } = string.Empty;
    public Guid? HeadId { get; set; } // Trưởng phòng / Quản đốc
    public bool IsActive { get; set; } = true;

    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}
