namespace ExpenseAgent.Services;

public class GmailLinkContext
{
    public bool NeedsLink { get; set; } = true;
    public string? Cookie { get; set; }
}