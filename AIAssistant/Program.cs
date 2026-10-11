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
using OpenAI.Responses;
using OpenAI.Files;
using OpenAI.Containers;
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
ResponsesClient responsesClient = openAIClient.GetResponsesClient();
OpenAIFileClient fileClient = openAIClient.GetOpenAIFileClient();
ContainerClient containerClient = openAIClient.GetContainerClient();

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


// Enable code interpreter tooling capability via the CreateResponseOptions,
// attaching the uploaded sales file directly to the tool's container.

// Configure the response options
CreateResponseOptions responseOptions = new(
    model,
    [
        ResponseItem.CreateUserMessageItem(
            "Read the uploaded monthly_sales.json file. Use the actual sales data " +
            "to create a graph of monthly units sold for each product. " +
            "You must use the Code Interpreter tool to execute Python code, " +
            "generate a real chart image, and return that image as an output file. " +
            "Do not describe how to create a graph; create it.")
    ])
{
    Instructions = "You are an assistant that looks up sales data and helps visualize the information based"
        + " on user queries. When asked to generate a graph, chart, or other visualization, use"
        + " the code interpreter tool to do so.",
    BackgroundModeEnabled = true, // Run the response asynchronously so we can poll for completion, mirroring the original run-polling flow.
};
responseOptions.Tools.Add(
    ResponseTool.CreateCodeInterpreterTool(
        CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration([salesFile.Id])));

// Create the response. With background mode enabled, the result starts out in a
// non-terminal status and must be polled, mirroring the original thread-run flow.
ResponseResult response = (await responsesClient.CreateResponseAsync(responseOptions)).Value;

// Send the prompt and monitor progress until the response is complete.
while (response.Status is ResponseStatus.Queued or ResponseStatus.InProgress)
{
    Thread.Sleep(TimeSpan.FromSeconds(1));
    response = (await responsesClient.GetResponseAsync(response.Id)).Value;
}

// Check if code interpreter actually ran
Console.WriteLine($"Response status: {response.Status}");
Console.WriteLine($"Response ID:     {response.Id}");

// Note: to continue this conversation in a follow-up turn, pass
// previousResponseId: response.Id on the next CreateResponseOptions.

//Print the messages and save the generated image from the conversation with assistant.

// Process the output items from the response
foreach (ResponseItem item in response.OutputItems)
{
    if (item is not MessageResponseItem message)
    {
        continue;
    }

    // Print the message content from the assistant
    Console.WriteLine($"[{message.Role.ToString().ToUpper()}]: ");
    foreach (ResponseContentPart contentItem in message.Content)
    {
        if (!string.IsNullOrEmpty(contentItem.Text))
        {
            Console.WriteLine(contentItem.Text);
            if (contentItem.OutputTextAnnotations.Count > 0)
            {
                Console.WriteLine();
            }

            // Include annotations, if any
            foreach (ResponseMessageAnnotation annotation in contentItem.OutputTextAnnotations)
            {
                if (annotation is FileCitationMessageAnnotation fileCitation)
                {
                    Console.WriteLine($"* File citation, file ID: {fileCitation.FileId}");
                }

                if (annotation is ContainerFileCitationMessageAnnotation containerFileCitation)
                {
                    Console.WriteLine($"* File output, new file ID: {containerFileCitation.FileId}");

                    // Save the generated image file from the code interpreter container
                    string filename = containerFileCitation.Filename ?? containerFileCitation.FileId;
                    BinaryData imageBytes = (await containerClient.DownloadContainerFileAsync(
                        containerFileCitation.ContainerId,
                        containerFileCitation.FileId)).Value;
                    using FileStream stream = File.OpenWrite($"{filename}.png");
                    imageBytes.ToStream().CopyTo(stream);
                    Console.WriteLine($"<image: {filename}.png");
                }
            }
        }
    }
    Console.WriteLine();
}