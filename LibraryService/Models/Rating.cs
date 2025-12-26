using System;
using System.Collections.Generic;

namespace LibraryService.Models;

public partial class Rating
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public int Stars { get; set; }
}
