/*
Core components of AI assistants
AI assistants are based around conversational threads 
with a user. The user sends prompts to the assistant on
a conversation thread, which direct the assistant to
complete tasks using the tools it has available.
Assistants can process and analyze data, make decisions, 
and interact with users or other systems to achieve
specific goals. Most assistants include the following 
components:

Component:	Description
Assistant:	The core AI client and logic that uses Azure 
            OpenAI models, manages conversation threads,
            and utilizes configured tools.
Thread:	A conversation session between an assistant and 
            a user. Threads store messages and automatically
            handle truncation to fit content into a model's context.
Message:	A message created by an assistant or a user. Messages can
            include text, images, and other files. Messages are stored
            as a list on the thread.
Run:	    Activation of an assistant to begin running based on the contents
            of the thread. The assistant uses its configuration and the thread's
            messages to perform tasks by calling models and tools. As part of
            a run, the assistant appends messages to the thread.
Run steps:	A detailed list of steps the assistant took as part of a run.
            An assistant can call tools or create messages during its run.
            Examining run steps allows you to understand how the assistant is
            getting to its final results.
*/

using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .Build();
string? apiKey = config["OpenAIKey"];
string? model = config["Model_gpt4o"];
//string? azureOpenAIEndpoint = config["AzureOpenAIEndpoint"];

// Create the OpenAI client
OpenAIClient openAIClient = new(apiKey);

//// For Azure OpenAI, use the following instead:
//AzureOpenAIClient azureAIClient = new(
//    new Uri(azureOpenAIEndpoint),
//    new DefaultAzureCredential());

#pragma warning disable OPENAI001
AssistantClient assistantClient = openAIClient.GetAssistantClient();
OpenAIFileClient fileClient = openAIClient.GetOpenAIFileClient();

// Create an in-memory document to upload to the file client
using Stream document = BinaryData.FromBytes("""
    {
        "description": "This document contains sales history for Contoso products.",
        "sales": [
            {
                "month": "January",
                "by_product": {
                    "113043": 15,
                    "113045": 12,
                    "113049": 2
                }
            },
            {
                "month": "February",
                "by_product": {
                    "113045": 22
                }
            },
            {
                "month": "March",
                "by_product": {
                    "113045": 16,
                    "113055": 5
                }
            }
        ]    
    }
    """u8.ToArray()).ToStream();

// Upload the document to the file client
OpenAIFile salesFile = fileClient.UploadFile(
    document,
    "monthly_sales.json",
    FileUploadPurpose.Assistants);


// Enable file search and code interpreter tooling capabilities via the AssistantCreateOptions.

// Configure the assistant options
AssistantCreationOptions assistantOptions = new()
{
    Name = "Example: Contoso Sales RAG",
    Instructions = "You are an assistant that looks up sales data and helps visualize the information based"
        + " on user queries. When asked to generate a graph, chart, or other visualization, use"
        + " the code interpreter tool to do so.",
    Tools =

    {
        new FileSearchToolDefinition(), // Enable the assistant to search and access files
        new CodeInterpreterToolDefinition() // Enables the assistant to run code for data analysis
    },
    ToolResources = new()
    {
        FileSearch = new()
        {
            NewVectorStores =
            {
                new VectorStoreCreationHelper([salesFile.Id]),
            }
        }
    },
};

// Create the Assistent and a thread to manage interactions between the user and assistant.
Assistant assistant = assistantClient.CreateAssistant(model, assistantOptions);

// Configure and create conversation thread.
ThreadCreationOptions threadOptions = new()
{
    //InitialMessages = { "How well did product 113045 sell in February? Graph its trend over time." }
    InitialMessages =
    {
        "Read the uploaded monthly_sales.json file. Use the actual sales data " +
        "to create a graph of monthly units sold for each product. " +
        "You must use the Code Interpreter tool to execute Python code, " +
        "generate a real chart image, and return that image as an output file. " +
        "Do not describe how to create a graph; create it."
    }
};

ThreadRun threadRun = assistantClient.CreateThreadAndRun(assistant.Id, threadOptions);

// Sent the prompt to the monitor pgress until the tread run is complete.
do {
    Thread.Sleep(TimeSpan.FromSeconds(1));
    threadRun = assistantClient.GetRun(threadRun.ThreadId, threadRun.Id);
} while (!threadRun.Status.IsTerminal);

// check if code interpreter actually run
Console.WriteLine($"Run status: {threadRun.Status}");
Console.WriteLine($"Thread ID:  {threadRun.ThreadId}");
Console.WriteLine($"Run ID:     {threadRun.Id}");
// Get the message from the thread run
var messages = assistantClient.GetMessagesAsync(
    threadRun.ThreadId,
    new MessageCollectionOptions()
    { Order = MessageCollectionOrder.Ascending }
    );

//Print the messages and save the generated image from the conversation with assistant.

// Process the messages from assistant
await foreach (ThreadMessage message in messages)
{
    // Print the message content from the assistant
    Console.WriteLine($"[{message.Role.ToString().ToUpper()}]: ");
    foreach (MessageContent contentItem in message.Content)
    {
        if (!string.IsNullOrEmpty(contentItem.Text))
        {
            Console.WriteLine(contentItem.Text);
            if (contentItem.TextAnnotations.Count > 0)
            {
                Console.WriteLine();
            }

            // Include annotations, if any
            foreach (TextAnnotation annotation in contentItem.TextAnnotations)
            {
                if (!string.IsNullOrEmpty(annotation.InputFileId))
                {
                    Console.WriteLine($"* File citation, file ID: {annotation.InputFileId}");
                }

                if (!string.IsNullOrEmpty(annotation.OutputFileId))
                {
                    Console.WriteLine($"* File output, new file ID: {annotation.OutputFileId}");
                }
            }
        }

        // Save the generated image file
        if (!string.IsNullOrEmpty(contentItem.ImageFileId))
        {
            OpenAIFile imageInfo = fileClient.GetFile(contentItem.ImageFileId);
            BinaryData imageBytes = fileClient.DownloadFile(contentItem.ImageFileId);
            using FileStream stream = File.OpenWrite($"{imageInfo.Filename}.png");
            imageBytes.ToStream().CopyTo(stream);
            Console.WriteLine($"<image: {imageInfo.Filename}.png");
        }
    }
    Console.WriteLine();

}