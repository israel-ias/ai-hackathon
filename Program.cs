using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container with proper HttpClient configuration
builder.Services.AddHttpClient("default", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // For development/testing environments, you can bypass certificate validation
    // In production, you should use proper certificates
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    }

    return handler;
});

// Also add a named HttpClient specifically for external APIs
builder.Services.AddHttpClient("external-apis", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // Handle certificate validation for external APIs
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
    {
        // In production, you might want to implement more sophisticated validation
        // For now, we'll accept all certificates to avoid the NotTimeValid error
        return true;
    };

    return handler;
});

// Add CORS for frontend development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure options
builder.Services.Configure<GitHubModelsOptions>(builder.Configuration.GetSection("GitHubModels"));
builder.Services.Configure<ExternalApisOptions>(builder.Configuration.GetSection("ExternalApis"));

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowAll");

// Serve static files from wwwroot folder
app.UseStaticFiles();

// Default route for serving the Vue app
app.MapGet("/", () => Results.Redirect("/index.html"));

// Endpoint 1: Get onboarding questions for a habit
app.MapGet("/questions/{habit}", async (string habit, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    var httpClient = httpClientFactory.CreateClient("external-apis");

    string onboardingQuestionsPrompt = $$"""
        You are an expert habit coach. 
        Your job is to ask concise onboarding questions to personalize a 7-day micro-habit plan. 
        Return ONLY valid minified JSON that exactly matches the required schema. 
        Do not include any extra commentary or markdown. Keep each question under 120 characters. 
        Use only types: \"text\" or \"single-choice\". 
        The questions should be practical and cover barriers, environment, motivation, and confidence. 
        Avoid asking for highly sensitive information. Use simple language.
        Generate at least 4 onboarding questions to personalize a 7-day plan about the habit: "{{habit}}". 
        Return JSON ONLY in this exact schema: {"questions":[ {"id":"q1","text":"...","type":"single-choice","options":["...","...","..."]}, {"id":"q2","text":"...","type":"text"}, {"id":"q3","text":"...","type":"single-choice","options":["...","...","..."]}, {"id":"q4","text":"...","type":"text"}, {"id":"q5","text":"...","type":"single-choice","options":["1","2","3","4","5"]} ]} 
        Only respond with the JSON object, nothing else.
        """;

    try
    {
        string result = await CallGitHubModelsApi(onboardingQuestionsPrompt, httpClient, configuration);
        var questions = JsonSerializer.Deserialize<OnboardingQuestionsResponse>(result);
        return Results.Ok(questions);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Endpoint 2: Generate 7-day plan based on habit and answers
app.MapPost("/plan", async (GeneratePlanRequest request, IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    var httpClient = httpClientFactory.CreateClient("external-apis");

    try
    {
        string answersJson = JsonSerializer.Serialize(request.Answers);

        string prompt2 = $$"""
            You are a Christian habit coach. 
            Create a 7-day micro-habit plan. 
            Each day must have exactly: one concrete micro-action that takes under 10 minutes, one brief reflection prompt (one sentence), exactly one Bible verse reference (reference only, no Bible text), and 1–2 quote tags to search for a famous quote. 
            Return ONLY valid minified JSON that matches the required schema. 
            Use actionable, specific steps (imperative voice). 
            Do not include any copyrighted Bible translation text—only references. 
            Prefer common references that are likely to exist in KJV (e.g., Proverbs, James, Colossians, Joshua, Luke, Philippians). 
            Keep language simple and encouraging.
            Create a personalized 7-day plan for the habit "{{request.Habit}}" using these answers: {{answersJson}}. Rules:
            microAction: one specific action doable in <10 minutes (e.g., "Define today's top 3 outcomes.")
            reflection: one sentence that invites self-examination (<=140 chars)
            verseRefs: exactly one Bible reference string like "Proverbs 21:5" (no text)
            quoteTags: 1–2 simple keywords for quotes (e.g., "discipline","focus","courage","stewardship") 
            Return JSON ONLY in this exact schema: {"planTitle":"...","daily":[ {"day":1,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1","tag2"]}, {"day":2,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1"]}, {"day":3,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1","tag2"]}, {"day":4,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1"]}, {"day":5,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1","tag2"]}, {"day":6,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1"]}, {"day":7,"microAction":"...","reflection":"...","verseRefs":["Book Chap:Verse"],"quoteTags":["tag1","tag2"]} ]} 
            Only respond with the JSON object, nothing else.
            """;

        string result = await CallGitHubModelsApi(prompt2, httpClient, configuration);
        var plan = JsonSerializer.Deserialize<PlanResponse>(result);

        // Enrich the plan with Bible verses and quotes
        var enrichedPlan = new EnrichedPlanResponse
        {
            PlanTitle = plan!.planTitle,
            Daily = new List<EnrichedDayPlan>()
        };

        foreach (var day in plan.daily)
        {
            var verseText = await CallBibleApi(day.verseRefs[0], httpClient, configuration);
            var quote = await CallQuoteApi(string.Join(" ", day.quoteTags), httpClient, configuration);

            enrichedPlan.Daily.Add(new EnrichedDayPlan
            {
                Day = day.day,
                MicroAction = day.microAction,
                Reflection = day.reflection,
                VerseReference = day.verseRefs[0],
                VerseText = verseText,
                Quote = quote?.content ?? "",
                QuoteAuthor = quote?.author ?? ""
            });
        }

        return Results.Ok(enrichedPlan);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static async Task<Quote?> CallQuoteApi(string tags, HttpClient httpClient, IConfiguration configuration)
{
    var apiUrl = configuration["ExternalApis:QuoteApi"] ?? "https://api.quotable.io/search/quotes?limit=1&query=";

    try
    {
        var response = await httpClient.GetAsync(apiUrl + tags);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<QuoteResponse>(responseContent);
            return apiResponse?.results.FirstOrDefault();
        }

        return null;
    }
    catch (Exception ex)
    {
        // Log the exception for debugging
        Console.WriteLine($"Quote API error: {ex.Message}");
        return null;
    }
}

static async Task<string> CallBibleApi(string verse, HttpClient httpClient, IConfiguration configuration)
{
    var apiUrl = configuration["ExternalApis:BibleApi"] ?? "https://bible-api.com/";

    try
    {
        var response = await httpClient.GetAsync(apiUrl + Uri.EscapeDataString(verse) + "?translation=kjv");

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<BibleResponse>(responseContent);
            var rawText = string.Join(" ", apiResponse?.verses.Select(v => v.text) ?? ["No verses found"]);

            // Clean up the text by removing escape characters and extra whitespace
            return CleanBibleText(rawText);
        }

        return "Bible verse not found";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Bible API error: {ex.Message}");
        return "Error retrieving Bible verse";
    }
}

static string CleanBibleText(string text)
{
    if (string.IsNullOrEmpty(text))
        return text;

    // Replace common escape characters with appropriate substitutes
    string cleanedText = text
        .Replace("\\n", " ")           // Replace \n with space
        .Replace("\\t", " ")           // Replace \t with space  
        .Replace("\\r", " ")           // Replace \r with space
        .Replace("\n", " ")            // Replace actual newlines with space
        .Replace("\t", " ")            // Replace actual tabs with space
        .Replace("\r", " ")            // Replace actual carriage returns with space
        .Replace("\\\"", "\"")         // Replace escaped quotes with normal quotes
        .Replace("\\'", "'")           // Replace escaped apostrophes with normal apostrophes
        .Replace("\\\\", "\\");        // Replace double backslashes with single backslash

    // Remove extra whitespace and normalize spacing
    cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"\s+", " ");

    // Trim leading and trailing whitespace
    return cleanedText.Trim();
}

static async Task<string> CallGitHubModelsApi(string userMessage, HttpClient httpClient, IConfiguration configuration, string? model = null)
{
    var apiToken = configuration["GitHubModels:ApiToken"];
    if (string.IsNullOrEmpty(apiToken))
    {
        throw new InvalidOperationException("GitHub Models API token is not configured. Please set GitHubModels:ApiToken in your configuration.");
    }

    var apiUrl = configuration["GitHubModels:ApiUrl"] ?? "https://models.github.ai/inference/chat/completions";
    var defaultModel = configuration["GitHubModels:DefaultModel"] ?? "xai/grok-3";
    var apiVersion = configuration["GitHubModels:ApiVersion"] ?? "2022-11-28";

    // Set up headers
    httpClient.DefaultRequestHeaders.Clear();
    httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
    httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", apiVersion);

    // Create the request payload
    var requestPayload = new
    {
        model = model ?? defaultModel,
        messages = new[]
        {
            new { role = "user", content = userMessage }
        }
    };

    var jsonContent = JsonSerializer.Serialize(requestPayload);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

    try
    {
        var response = await httpClient.PostAsync(apiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<GitHubModelsResponse>(responseContent);

            var aiResponse = apiResponse?.choices?.FirstOrDefault()?.message?.content ?? "No response received";

            return aiResponse;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"API call failed with status {response.StatusCode}: {errorContent}");
        }
    }
    catch (Exception ex)
    {
        throw new Exception($"Exception occurred: {ex.Message}");
    }
}

// Configuration models
public class GitHubModelsOptions
{
    public string ApiToken { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://models.github.ai/inference/chat/completions";
    public string DefaultModel { get; set; } = "xai/grok-3";
    public string ApiVersion { get; set; } = "2022-11-28";
}

public class ExternalApisOptions
{
    public string QuoteApi { get; set; } = "https://api.quotable.io/search/quotes?limit=1&query=";
    public string BibleApi { get; set; } = "https://bible-api.com/";
}

// Request/Response models
public record GeneratePlanRequest(string Habit, Dictionary<string, string> Answers);

public record EnrichedPlanResponse
{
    public string PlanTitle { get; set; } = string.Empty;
    public List<EnrichedDayPlan> Daily { get; set; } = new();
}

public record EnrichedDayPlan
{
    public int Day { get; set; }
    public string MicroAction { get; set; } = string.Empty;
    public string Reflection { get; set; } = string.Empty;
    public string VerseReference { get; set; } = string.Empty;
    public string VerseText { get; set; } = string.Empty;
    public string? Quote { get; set; }
    public string? QuoteAuthor { get; set; }
}

// Existing models
public record GitHubModelsResponse(
    string id,
    string @object,
    long created,
    string model,
    Choice[] choices,
    Usage usage
);

public record Choice(
    int index,
    Message message,
    object? logprobs,
    string finish_reason
);

public record Message(
    string role,
    string content
);

public record Usage(
    int prompt_tokens,
    int completion_tokens,
    int total_tokens
);

public record OnboardingQuestionsResponse(
    List<Question> questions
);

public record Question(string id, string text, string type, List<string>? options);

public record QuestionAnswer(string Id, string Answer);

public record BibleVerse(string book_id, string book_name, int chapter, int verse, string text);
public record BibleResponse(string reference, List<BibleVerse> verses, string text, string translation_id, string translation_name, string translation_note);

public record DayPlan(int day, string microAction, string reflection, List<string> verseRefs, List<string> quoteTags);
public record PlanResponse(string planTitle, List<DayPlan> daily);

public record Quote(string _id, string content, string author, List<string> tags, string authorId, string authorSlug, int length, DateOnly dateAdded, DateOnly dateModified);

public record QuoteResponse(int count, int totalCount, int page, int totalPages, List<Quote> results);