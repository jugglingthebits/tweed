using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NodaTime;
using Tweed.Data;
using Tweed.Data.Models;

namespace Tweed.Web.Pages;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ITweedQueries _tweedQueries;
    private readonly UserManager<AppUser> _userManager;

    public CreateModel(ITweedQueries tweedQueries, UserManager<AppUser> userManager)
    {
        _tweedQueries = tweedQueries;
        _userManager = userManager;
    }

    [BindProperty]
    [Required]
    [StringLength(280)]
    public string? Text { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = _userManager.GetUserId(User);
        var now = SystemClock.Instance.GetCurrentInstant().InUtc();
        var tweed = new Data.Models.Tweed
        {
            Text = Text,
            CreatedAt = now,
            AuthorId = userId
        };
        await _tweedQueries.StoreTweed(tweed);

        return RedirectToPage("./index");
    }
}
