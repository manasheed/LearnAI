using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using StructuredOutput;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();
string apiKey = config["OpenAIKey"]!;
string model = config["ModelName"]!;

// get chat client
var chatClient = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsIChatClient();

string[] inputs = [
    "I'm happy with the product!",
    "The product is okay, but it could be better.",
    "I'm disappointed with the product.",
    "Hello",
    "Returned it immediately"
];

foreach(var i in inputs)
{
    var response = await chatClient.GetResponseAsync<Sentiment>(
        $"What's the sentiment of the review? {i}");
    Console.WriteLine($"Review: {i}");
    Console.WriteLine($"Sentiment: {response.Result}");
    Console.WriteLine();
}


string review2 = "The product is okay, but it could be better.";
var response2 = await chatClient.GetResponseAsync<SentimentRecord>(
    $"What's the sentiment of the review? {review2}");

Console.WriteLine($"Rsponse text: {response2.Result.ResponseText}");
Console.WriteLine($"Sentiment: {response2.Result.ReviewSentiment}");
Console.WriteLine();