using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string model = config["ModelName"]!;
string apiKey = config["OpenAIKey"]!;

// Create the IChatClient

IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient();

// Start converstion with context for the AI model

List<ChatMessage> chatHistory =
    [
        new ChatMessage(ChatRole.System, """
        You are a friendly hiking enthusiastic who helps people discover fun hikes in their area.
        You introduce yourself when first saying hello.
        When helping piople out, you always ask them for this information
        to inform the hiking recommendations you provide:
        1. The location where they would like to hike
        2. What hiking inensity they are looking for
        
        You will then provide three suggestions for nearby hikes that vary in length
        after you get that information. You will also share an interesting fact about
        the local nature on the hikes when making a recommendation. At the end of your
        response, ask if there is anything else you can help with.
        """),
    ];

// Loop to get user inputand and stream AI resposne
while (true)
{
    // Get user prompt and add to chat history
    Console.WriteLine("Your prompt: ");
    string? userPrompt = Console.ReadLine();
    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

    // Sream the AI response add to chat history
    Console.WriteLine("AI response: ");
    string response = "";
    await foreach (ChatResponseUpdate item in 
        chatClient.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        response += item.Text;
    }

    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}