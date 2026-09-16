using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Infrastructure.Data;
using System.Threading.Tasks;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationController : ControllerBase
{
    private readonly CongTacDangDbContext _db;

    public OrganizationController(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách các Chi bộ thuộc Đảng bộ ATTECH</summary>
    [HttpGet("party-cells")]
    public async Task<IActionResult> GetPartyCells()
    {
        var cells = await _db.PartyCells
            .Include(c => c.Members)
            .OrderBy(c => c.Code)
            .ToListAsync();
        return Ok(cells);
    }

    /// <summary>Lấy danh sách các Phòng ban / Xưởng sản xuất chính quyền</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var deps = await _db.AdministrativeDepartments
            .OrderBy(d => d.Code)
            .ToListAsync();
        return Ok(deps);
    }

    /// <summary>Lấy danh sách cán bộ / đảng viên theo Chi bộ hoặc Đơn vị</summary>
    [HttpGet("members")]
    public async Task<IActionResult> GetMembers([FromQuery] System.Guid? cellId, [FromQuery] System.Guid? depId)
    {
        var query = _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .AsQueryable();

        if (cellId.HasValue) query = query.Where(m => m.PartyCellId == cellId);
        if (depId.HasValue) query = query.Where(m => m.DepartmentId == depId);

        var members = await query.ToListAsync();
        return Ok(members);
    }
}
