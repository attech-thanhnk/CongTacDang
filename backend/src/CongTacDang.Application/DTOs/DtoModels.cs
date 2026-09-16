using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PartyRole { get; set; } = string.Empty;
    public string AdminTitle { get; set; } = string.Empty;
    public string PartyBranchName { get; set; } = string.Empty;
    public string AdminDeptName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public class CadreDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PartyCardNumber { get; set; }
    public string? PartyRole { get; set; }
    public string? AdminTitle { get; set; }
    public string? PartyCellName { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsPartyMember { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PartyCardNumber { get; set; }
    public string? AdminTitle { get; set; }
    public Guid? PartyCellId { get; set; }
    public Guid? DepartmentId { get; set; }
}

public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PartyCardNumber { get; set; }
    public string? AdminTitle { get; set; }
    public Guid? PartyCellId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
}

public class RoleDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BranchDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class CreateBranchDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UpdateBranchDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class AttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public string Provider { get; set; } = "local";
    public string? DownloadUrl { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

public class UpdateAttachmentDto
{
    public string? Category { get; set; }
    public string? Description { get; set; }
}
