using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;

namespace WebLibrary.Pages.Books
{
    public class IndexModel : PageModel
    {
        // ��̬�ڴ��б�ģ�����ݿ�
        public static List<BookItem> BookList { get; set; } = new List<BookItem>();

        [BindProperty]
        public string Title { get; set; }
        [BindProperty]
        public string Author { get; set; }

        public List<BookItem> Books => BookList;

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Author))
            {
                BookList.Add(new BookItem { Id = System.Guid.NewGuid().ToString(), Title = Title, Author = Author });
            }
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(string id)
        {
            var book = BookList.FirstOrDefault(b => b.Id == id);
            if (book != null)
            {
                BookList.Remove(book);
            }
            return RedirectToPage();
        }

        public class BookItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
        }
    }
}
