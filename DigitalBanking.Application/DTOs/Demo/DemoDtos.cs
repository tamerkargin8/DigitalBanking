namespace DigitalBanking.Application.DTOs.Demo;

public class DemoScenarioDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StartState { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
    public int? RelatedPaymentId { get; set; }
    public string? SampleDownloadUrl { get; set; }
}

public class DemoSeedResultDto
{
    public bool AlreadySeeded { get; set; }
    public int CreatedPayments { get; set; }
    public List<DemoScenarioDto> Scenarios { get; set; } = new();
}

public class DemoResetResultDto
{
    public int RemovedPayments { get; set; }
    public int RemovedTransactions { get; set; }
    public int RemovedTimelineEvents { get; set; }
    public int RemovedBatches { get; set; }
    public string Message { get; set; } = string.Empty;
}
