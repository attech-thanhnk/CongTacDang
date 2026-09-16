using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Api.Controllers
{
    public class RoleDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly CongTacDangDbContext _db;

        public UserController(CongTacDangDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Lay ho so 2 vai va quyen han cua can bo dang dang nhap
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile([FromQuery] string username = "cb_nguyenvana")
        {
            var member = await _db.PartyMemberProfiles
                .Include(m => m.PartyCell)
                .Include(m => m.Department)
                .FirstOrDefaultAsync(m => m.Username == username);

            if (member == null)
            {
                return Ok(new
                {
                    id = Guid.NewGuid(),
                    fullName = "Nguyen Van A",
                    userName = "cb_nguyenvana",
                    partyRole = "Bi thu Chi bo",
                    adminTitle = "Truong phong Ke hoach KD",
                    partyBranchName = "Chi bo Khoi Ky thuat",
                    adminDeptName = "Phong Ke hoach Kinh doanh",
                    jobGroup = "Khung 2 (Ky thuat - Nghiep vu CNS)",
                    roles = new[] { "CAN_BO", "BI_THU_CHI_BO" }
                });
            }

            return Ok(new
            {
                id = member.Id,
                fullName = member.FullName,
                userName = member.Username,
                partyRole = member.PartyRole.ToString(),
                adminTitle = member.PositionTitle,
                partyBranchName = member.PartyCell?.Name,
                adminDeptName = member.Department?.Name,
                jobGroup = member.JobGroup.ToString(),
                roles = new[] { "CAN_BO", "BI_THU_CHI_BO" }
            });
        }

        /// <summary>
        /// Danh sach toan bo 68 can bo quan ly ATTECH (Quan tri nhan su 2 vai & RBAC)
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetUserList()
        {
            var members = await _db.PartyMemberProfiles
                .Include(m => m.PartyCell)
                .Include(m => m.Department)
                .Select(m => new
                {
                    m.Id,
                    m.FullName,
                    userName = m.Username,
                    m.PartyCardNumber,
                    PartyRole = m.PartyRole.ToString(),
                    AdminTitle = m.PositionTitle,
                    BranchName = m.PartyCell != null ? m.PartyCell.Name : "",
                    DepartmentName = m.Department != null ? m.Department.Name : "",
                    IsActive = m.IsActive
                })
                .ToListAsync();

            return Ok(members);
        }

        /// <summary>
        /// Danh muc 5 Role he thong theo quy trinh 03-HD/TVDU
        /// </summary>
        [HttpGet("roles")]
        public IActionResult GetSystemRoles()
        {
            var roles = new List<RoleDto>
            {
                new RoleDto { Code = "CAN_BO", Name = "Can bo Lanh dao / Quan ly", Description = "Dang ky nhiem vu (Mau 01), Tu danh gia (Mau 02), Bo phieu kin chi bo (Mau 11)" },
                new RoleDto { Code = "BI_THU_CHI_BO", Name = "Bi thu / Cap uy Chi bo", Description = "Theo doi tien do chi bo, Chu tri bo phieu kin, Ky Bien ban kiem phieu (Mau 12, 13)" },
                new RoleDto { Code = "TO_THAM_DINH", Name = "To Tham dinh Dang uy", Description = "Tham dinh ho so (Mau 03), Lap phieu tham dinh Mau 10 khi chenh lech >= 5d, Soat tran 20%" },
                new RoleDto { Code = "BAN_THUONG_VU", Name = "Ban Thuong vu Dang uy", Description = "Xem xet danh sach tong hop (Mau 14, 15), Quyet dinh xep loai chinh thuc, Ky bao cao Mau 16" },
                new RoleDto { Code = "ADMIN_HE_THONG", Name = "Quan tri vien He thong", Description = "Quan tri danh muc 2 vai, Thiet lap cau hinh thang diem va tran ty le, Quan tri phan quyen RBAC" }
            };

            return Ok(roles);
        }
    }
}
