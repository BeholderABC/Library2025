using System.ComponentModel.DataAnnotations;

namespace WebLibrary.Pages.Shared.Models
{
    public class Lecture
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "讲座名称不能为空")]
        [StringLength(255, ErrorMessage = "讲座名称不能超过255个字符")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "讲座日期不能为空")]
        public DateTime LectureDate { get; set; }
        
        [Required(ErrorMessage = "主讲人不能为空")]
        [StringLength(255, ErrorMessage = "主讲人姓名不能超过255个字符")]
        public string Speaker { get; set; } = string.Empty;
        
        public string? Summary { get; set; }
        
        public byte[]? Picture { get; set; }
        
        [Required(ErrorMessage = "最大人数不能为空")]
        [Range(1, 1000, ErrorMessage = "最大人数必须在1-1000之间")]
        public int MaxNum { get; set; } = 100;
        
        [Required(ErrorMessage = "当前人数不能为空")]
        [Range(0, 1000, ErrorMessage = "当前人数必须在0-1000之间")]
        public int NowNum { get; set; } = 0;
    }
} 