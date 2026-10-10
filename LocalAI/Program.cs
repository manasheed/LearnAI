/*
Open a terminal window and verify that Ollama is available on your device:

ollama
If Ollama is available, it displays a list of available commands.

Start Ollama:
ollama serve
If Ollama is running, it displays a list of available commands.

Pull the phi3:mini model from the Ollama registry and wait for it to download:

ollama pull phi3:mini
After the download completes, run the model:

ollama run phi3:mini
Ollama starts the phi3:mini model and provides a prompt for you to interact with it.  
*/

using Microsoft.Extensions.AI;
using OllamaSharp;

IChatClient chatClient = new OllamaApiClient(
    new Uri("http://localhost:11434"), "phi3:mini");

// Start the conversation with context for the model.
List<ChatMessage> chatHistory = new ();

while(true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();
    chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

    // Send the conversation to the model and get and add response to chat history.
    var response = string.Empty;
    await foreach (ChatResponseUpdate item in 
        chatClient.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        response += item.Text;
    }
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
    Console.WriteLine();
}

