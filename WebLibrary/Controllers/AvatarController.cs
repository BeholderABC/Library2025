using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Scroll.Database;
using System.Data;

namespace WebLibrary.Controllers
{
    using UserType = Int64;

    [ApiController]
    [Route("api/[controller]")]
    public class AvatarController : ControllerBase
    {
        private readonly IScrollDB _repository;

        public AvatarController(IScrollDB repository)
        {
            _repository = repository;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetAvatar(UserType userId)
        {
            byte[]? avatarBytes = await _repository.GetAvatarAsync(userId);

            // 返回图片，并指定 MIME 类型
            if (avatarBytes == null || avatarBytes.Length == 0)
                return NotFound("Avatar not found.");
            return File(avatarBytes, "image/jpeg");
        }

        [HttpPost("{userId}")]
        public async Task<IActionResult> ChangeAvatar(UserType userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // 将 IFormFile 转换为 byte[]
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }
            bool success = await _repository.ChangeAvatarAsync(userId, imageBytes);

            if (success)
                return Ok("Avatar updated successfully.");
            else
                return StatusCode(500, "Failed to update avatar.");
        }
    }
}
