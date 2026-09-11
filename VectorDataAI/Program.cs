using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using OpenAI;
using OpenAI.Realtime;
using System.ClientModel;
using VectorDataAI;

List<CloudService> cloudServices =
[
    new ()
    {
        Key = 0,
        Name = "Azure App Services",
        Description = "Host .NET, Java, Node.js, and Python web applications and APIs in a fully managed Azure service. You only need to deploy your code to Azure. Azure takes care of all the infrastructure management like high availability, load balancing, and autoscaling."
    },
    new()
    {
        Key = 1,
        Name = "Azure Cognitive Services",
        Description = "A collection of AI services and APIs that help developers build intelligent applications.",
    },
    new()
    {
        Key = 2,
        Name = "AWS AI Services",
        Description = "A suite of machine learning and AI services provided by Amazon Web Services.",
    },
    new()
    {
        Key = 3,
        Name = "Google Cloud AI",
        Description = "A set of machine learning and AI tools offered by Google Cloud Platform.",
    },
    new() 
    {
        Key = 4,
        Name = "Azure Key Vault",
        Description = "Store and access application secrets like connection strings and API keys in an encrypted vault with restricted access to make sure your secrets and your application aren't compromised."
    },
    new() 
    {
        Key = 5,
        Name = "Azure AI Search",
        Description = "Information retrieval at scale for traditional and conversational search applications, with security and options for AI enrichment and vectorization."
    },
    new()
    {
        Key = 6,
        Name = "Azure Functions",
        Description = "A serverless compute service that enables you to run event-driven code without having to explicitly provision or manage infrastructure."            
    }
];

// Load the configuration values.

IConfigurationRoot config = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .Build();

string model = config["EbeddingModelName"];
string key = config["OpenAIKey"];

// Create the embedding generator.
IEmbeddingGenerator<string, Embedding<float>> generator =
    new OpenAIClient(new ApiKeyCredential(key))
    .GetEmbeddingClient(model:model)
    .AsIEmbeddingGenerator();

//Create and populate the vector store.
var vectorStore = new InMemoryVectorStore();
VectorStoreCollection<int, CloudService> cloudServicesStore =
    vectorStore.GetCollection<int, CloudService>(name: "cloudServices");
await cloudServicesStore.EnsureCollectionExistsAsync();
foreach(CloudService service in cloudServices)
{
    service.Vector = await generator.GenerateVectorAsync(service.Description);
    await cloudServicesStore.UpsertAsync(service);
}

// Create a search query to vector
// and serch the vector store.
string query = "Whchi Azure service should I use to trigger sms sender?";
ReadOnlyMemory<float> queryEmbedding = await generator.GenerateVectorAsync(query);
IAsyncEnumerable<VectorSearchResult<CloudService>> results =
    cloudServicesStore.SearchAsync(queryEmbedding, top: 3);

await foreach (VectorSearchResult<CloudService> result in results)
{
    Console.WriteLine($"Key: {result.Record.Key}");
    Console.WriteLine($"Name: {result.Record.Name}");
    Console.WriteLine($"Description: {result.Record.Description}");
    Console.WriteLine($"Vector match score: {result.Score}");
}