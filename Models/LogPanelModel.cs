namespace ApiMocker.Models;

public class LogPanelModel
{
    public string Title { get; set; } = "";
    public string TitleIcon { get; set; } = "bi-file-text";
    public string TitleColor { get; set; } = "#94a3b8";
    public string? BadgeText { get; set; }
    public string? BadgeColor { get; set; }

    public int? StatusCode { get; set; }

    public string? Headers { get; set; }
    public string? Body { get; set; }

    // When set, changed headers/body get highlighted
    public bool HighlightChanges { get; set; } = false;
    public string? CompareHeaders { get; set; }
    public string? CompareBody { get; set; }
}
