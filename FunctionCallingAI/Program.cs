using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .Build();

string? model = config["ModelName"];
string? apiKey = config["OpenAIKey"];

IChatClient client = new ChatClientBuilder(
    new OpenAIClient(apiKey)
    .GetChatClient(model ?? "gpt-5").AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// Add a new plugin with a local .NET function
// that should be available to the AI model.
var chatOptions = new ChatOptions()
{
    Tools = [AIFunctionFactory.Create((
        string location, string unit) =>
    { 
        // Here you would call a weather API
        // to get the weather for a location
        return "Periods of rain or drizzle, 15 C";
    },
    "get_current_weather",
    "Get the current weather in a given location"
    )]
};

// System prompt to provide context to the AI model about the available function.
List<ChatMessage> chatHistory = [new(ChatRole.System, """
    You are a hiking ethusiast who helps people discover fun hikes in their 
    area. You have access to a function called get_current_weather 
    that takes a location and a unit (C or F) and returns the current weather 
    for that location. You should use this function to get the current weather
    for the location the user is asking about. You are upbeat and friendly.
    """)];

//Weather conversation with relevant to the regisered function.
chatHistory.Add(new ChatMessage(
    ChatRole.User, "I live in Montreal and I'm looking for a moderate intensity hike. What's the current weather like?"));

Console.WriteLine($"{chatHistory.Last().Role} >>> {chatHistory.Last()}");

ChatResponse response = await client.GetResponseAsync(chatHistory, chatOptions);
Console.WriteLine($"Assistant >>> {response.Text}");