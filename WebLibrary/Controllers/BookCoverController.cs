using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Scroll.Database;
using System.Data;

namespace WebLibrary.Controllers
{
    using BookType = Int64;

    [Route("api/[controller]")]
    [ApiController]
    public class BookCoverController : ControllerBase
    {
        private readonly IScrollDB _repository;

        public BookCoverController(IScrollDB repository)
        {
            _repository = repository;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCoverUrl(BookType id)
        {
            //Console.WriteLine($"图片 GetCoverAsync({id})");
            var image = await _repository.GetCoverAsync(id);
            if (image == null) 
                return NotFound();
            //System.IO.File.WriteAllBytes(@$"F:\test{id}.jpg", image);
            return File(image, "image/jpeg");
        }
    }
}
