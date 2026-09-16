using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttachmentController : ControllerBase
    {
        private readonly CongTacDangDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly string _uploadFolder;

        // Các định dạng tệp minh chứng được phép
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png" };
        private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

        public AttachmentController(CongTacDangDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
            _uploadFolder = Path.Combine(_env.ContentRootPath, "storage", "attachments");
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        /// <summary>
        /// Tải lên tệp minh chứng (PDF, DOCX, XLSX, Ảnh) cho nhiệm vụ hoặc giải trình
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(30 * 1024 * 1024)]
        public async Task<IActionResult> UploadFile(
            [FromForm] IFormFile file,
            [FromForm] Guid? taskId,
            [FromForm] Guid? recordId,
            [FromForm] string formCode = "M01",
            [FromForm] string description = "")
        {
            if (file == null || file.Length == 0)
                return BadRequest("Tệp đính kèm không được để trống.");

            if (file.Length > MaxFileSize)
                return BadRequest("Dung lượng tệp vượt quá giới hạn tối đa 25MB.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return BadRequest($"Định dạng tệp '{ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.");

            // Đặt tên tệp duy nhất để chống ghi đè và path traversal
            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var savePath = Path.Combine(_uploadFolder, uniqueFileName);

            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new TaskAttachment
            {
                FileName = uniqueFileName,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                FilePath = savePath,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = "Nguyễn Văn A", // Demo user
                TaskId = taskId,
                RecordId = recordId,
                FormCode = formCode,
                Description = description
            };

            _db.TaskAttachments.Add(attachment);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = attachment.Id,
                originalFileName = attachment.OriginalFileName,
                fileSize = attachment.FileSize,
                uploadedAt = attachment.UploadedAt,
                formCode = attachment.FormCode,
                message = "Tải lên tệp minh chứng thành công!"
            });
        }

        /// <summary>
        /// Tải xuống tệp minh chứng theo ID
        /// </summary>
        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadFile(Guid id)
        {
            var attachment = await _db.TaskAttachments.FindAsync(id);
            if (attachment == null || !System.IO.File.Exists(attachment.FilePath))
                return NotFound("Không tìm thấy tệp minh chứng yêu cầu.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(attachment.FilePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, attachment.ContentType, attachment.OriginalFileName);
        }

        /// <summary>
        /// Lấy danh sách tệp minh chứng của một nhiệm vụ
        /// </summary>
        [HttpGet("by-task/{taskId}")]
        public async Task<IActionResult> GetByTaskId(Guid taskId)
        {
            var list = await _db.TaskAttachments
                .Where(a => a.TaskId == taskId)
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new
                {
                    a.Id,
                    a.OriginalFileName,
                    a.FileSize,
                    a.ContentType,
                    a.UploadedAt,
                    a.UploadedBy,
                    a.FormCode,
                    a.Description
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>
        /// Xóa tệp minh chứng
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFile(Guid id)
        {
            var attachment = await _db.TaskAttachments.FindAsync(id);
            if (attachment == null)
                return NotFound("Không tìm thấy tệp.");

            if (System.IO.File.Exists(attachment.FilePath))
            {
                System.IO.File.Delete(attachment.FilePath);
            }

            _db.TaskAttachments.Remove(attachment);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xóa tệp minh chứng thành công." });
        }
    }
}
