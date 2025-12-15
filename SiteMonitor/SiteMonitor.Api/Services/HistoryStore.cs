using System.Text.Json;

namespace SiteMonitor.Api.Services;

public class HistoryStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HistoryStore(IHostEnvironment hostEnvironment)
    {
        var dataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "scan-history.json");
    }

    public async Task AddRecordAsync(ScanRecord record)
    {
        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsInternalAsync();
            records.Add(record);
            await SaveRecordsInternalAsync(records);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ScanRecord>> GetHistoryAsync(string normalizedUrl)
    {
        await _lock.WaitAsync();
        try
        {
            var records = await LoadRecordsInternalAsync();
            return records
                .Where(r => string.Equals(r.Url, normalizedUrl, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.TimestampUtc)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ScanRecord?> GetLatestAsync(string normalizedUrl)
    {
        var history = await GetHistoryAsync(normalizedUrl);
        return history.FirstOrDefault();
    }

    private async Task<List<ScanRecord>> LoadRecordsInternalAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<ScanRecord>();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ScanRecord>();
        }

        return JsonSerializer.Deserialize<List<ScanRecord>>(json, _serializerOptions) ?? new List<ScanRecord>();
    }

    private async Task SaveRecordsInternalAsync(List<ScanRecord> records)
    {
        var json = JsonSerializer.Serialize(records, _serializerOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
