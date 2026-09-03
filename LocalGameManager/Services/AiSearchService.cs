using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalGameManager.ViewModels;

namespace LocalGameManager.Services;

public sealed class AiSearchPlan
{
    [JsonConverter(typeof(FlexibleStringListConverter))]
    public List<string> RequiredFields { get; set; } = [];
}

public sealed class AiSearchResult
{
    [JsonConverter(typeof(FlexibleLongListConverter))]
    public List<long> GameIds { get; set; } = [];
}

public sealed class FlexibleStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return [];
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrWhiteSpace(value) ? [] : [value];
        }
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("应为字符串或字符串数组。");
        var values = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value) values.Add(value);
        return values;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);
}

public sealed class FlexibleLongListConverter : JsonConverter<List<long>>
{
    public override List<long> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return [];
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("gameIds 应为数组。");
        var values = new List<long>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number)) values.Add(number);
            else if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out number)) values.Add(number);
        }
        return values;
    }

    public override void Write(Utf8JsonWriter writer, List<long> value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);
}

public sealed class AiSearchService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "originalName", "translatedName", "clubName", "authors", "illustrators", "voiceActors", "tagNames",
        "rating", "fileSizeBytes", "addedAtUtc", "lastLaunchedAtUtc", "totalPlaySeconds", "launchCount",
        "review", "description", "gameEngine", "releaseDate",
        "isNew", "isFavorite", "isCompleted", "isTranslated", "isUncensored", "isAnimated", "hasVoice"
    };

    public async Task<IReadOnlyList<long>> SearchAsync(string request, IEnumerable<GameListItem> games, IEnumerable<string> availableTags, AiSettings settings, CancellationToken cancellationToken = default)
    {
        var gameList = games.ToList();
        var apiKey = GetApiKey(settings);
        var plan = await GetPlanAsync(request, availableTags, settings, apiKey, cancellationToken);
        var records = gameList.Select(game => BuildRecord(game, plan.RequiredFields)).ToList();
        var result = await GetResultAsync(request, records, settings, apiKey, cancellationToken);
        var validIds = gameList.Select(game => game.Id).ToHashSet();
        return result.GameIds.Where(validIds.Contains).Distinct().ToList();
    }

    private async Task<AiSearchPlan> GetPlanAsync(string request, IEnumerable<string> availableTags, AiSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        var fields = string.Join("、", AllowedFields.Order());
        var system = $"你是本地游戏库搜索的字段规划器。只返回 JSON 对象，不要解释，格式为 {{\"requiredFields\":[\"字段名\"]}}。根据用户搜索需求，只选择完成筛选、排序、限制所必需的字段。只能从以下白名单选择：{fields}。ID 会由程序自动附带，不能写入 requiredFields。名称搜索通常需要 originalName 和 translatedName；标签条件需要 tagNames；涉及多个状态时选择对应的 is... 字段。description（作品内容）是高文本量字段：只有用户明确提到“作品内容”“剧情”“故事”“介绍”或“设定”时才允许选择；普通推荐、评分、标签、名称、状态、日期、容量等搜索绝不能选择 description。无法理解时返回空数组。";
        var user = $"用户请求：{request}\n可用标签（仅用于判断 tagNames 是否需要；标签名称在下一步的游戏数据中提供）：{string.Join("、", availableTags.Distinct(StringComparer.OrdinalIgnoreCase))}";
        var plan = await SendAsync<AiSearchPlan>(system, user, settings, apiKey, 300, cancellationToken);
        plan.RequiredFields = (plan.RequiredFields ?? []).Where(AllowedFields.Contains).Distinct(StringComparer.Ordinal).ToList();
        return plan;
    }

    private async Task<AiSearchResult> GetResultAsync(string request, IReadOnlyList<Dictionary<string, object?>> records, AiSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        var system = "你是本地游戏库搜索执行器。只返回 JSON 对象，不要解释，格式必须为 {\"gameIds\":[数字ID,...]}。根据用户请求，从提供的游戏记录中选择匹配项，并按用户要求排序。返回的 ID 必须来自记录中的 id；不得杜撰、重复或返回未匹配项。没有结果时返回 {\"gameIds\":[]}。";
        var user = $"用户请求：{request}\n游戏记录：{JsonSerializer.Serialize(records)}";
        return await SendAsync<AiSearchResult>(system, user, settings, apiKey, 1000, cancellationToken);
    }

    private static Dictionary<string, object?> BuildRecord(GameListItem game, IReadOnlyCollection<string> fields)
    {
        var record = new Dictionary<string, object?> { ["id"] = game.Id };
        foreach (var field in fields)
        {
            record[field] = field switch
            {
                "originalName" => game.OriginalName,
                "translatedName" => game.TranslatedName,
                "clubName" => game.ClubName,
                "authors" => game.Authors,
                "illustrators" => game.Illustrators,
                "voiceActors" => game.VoiceActors,
                "tagNames" => game.TagNames,
                "rating" => game.Rating,
                "fileSizeBytes" => game.FileSizeBytes,
                "addedAtUtc" => game.AddedAtUtc,
                "lastLaunchedAtUtc" => game.LastLaunchedAtUtc,
                "totalPlaySeconds" => game.TotalPlaySeconds,
                "launchCount" => game.LaunchCount,
                "review" => game.Review,
                "description" => game.Description,
                "gameEngine" => game.GameEngine,
                "releaseDate" => game.ReleaseDate?.ToString("yyyy-MM-dd"),
                "isNew" => game.IsNew,
                "isFavorite" => game.IsFavorite,
                "isCompleted" => game.IsCompleted,
                "isTranslated" => game.IsTranslated,
                "isUncensored" => game.IsUncensored,
                "isAnimated" => game.IsAnimated,
                "hasVoice" => game.HasVoice,
                _ => null
            };
        }
        return record;
    }

    private static string GetApiKey(AiSettings settings)
    {
        var keyName = settings.ApiKeyEnvironmentVariable.Trim();
        var apiKey = string.IsNullOrWhiteSpace(keyName) ? null : Environment.GetEnvironmentVariable(keyName);
        return string.IsNullOrWhiteSpace(apiKey) ? throw new InvalidOperationException($"找不到环境变量 {keyName} 中的 AI API Key。") : apiKey;
    }

    private static async Task<T> SendAsync<T>(string system, string user, AiSettings settings, string apiKey, int maxTokens, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = settings.Model,
            messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            temperature = 0.1,
            max_tokens = maxTokens,
            response_format = new { type = "json_object" },
            thinking = new { type = "disabled" }
        };
        var endpoint = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await Client.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"AI 服务返回 {(int)response.StatusCode}：{responseBody}");
        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("AI 没有返回结果。");
        return JsonSerializer.Deserialize<T>(content, Json) ?? throw new InvalidOperationException("AI 返回的 JSON 无法识别。");
    }
}
