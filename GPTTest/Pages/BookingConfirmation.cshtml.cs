using GPTTest.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GPTTest.Pages;

public class BookingConfirmation : PageModel
{
    private readonly GptTestContext _context;
    public Appointment? Appointment { get; set; }

    public BookingConfirmation(GptTestContext context)
    {
        _context = context;
    }

    public void OnGet([FromRoute] int? id)
    {
        if (id is not null)
        {
            Appointment = _context.Appointments?.FirstOrDefault(a => a.Id == id);
        }
    }
}