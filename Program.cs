using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Experimental.Orchestration;

class SemanticKernelDemo
{
    static async Task Main(string[] args)
    {

        //////////////////
        // Hard-coded Azure OpenAI credentials
        string deploymentName = "gpt-4o-mini";
        string azureEndpoint = "https://openaiserviceautogen04.openai.azure.com/";

        //////////REPLACE WITH YOU OWN KEY.You can use environment varialbe to store your key or hardcode it here///////////////////
        //string apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        string apiKey = "xxxxxxxYOUR AZURE OPENAI SERVICE_API_KEY GOES HERE";
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("The Azure OpenAI API key is not set");
        }

        // Create the kernel
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName,
            endpoint: azureEndpoint,
            apiKey: apiKey
        );
        var kernel = builder.Build();
        /////////////////

        // Define the agent prompts with input placeholders
        var TravelReporterPrompt = "You are a helpful AI assistant who finds discounted travel deals: {{$input}}. Check known travel sites and Keep your output concise.";
        var TravelEditorPrompt = "You are a helpful AI assistant that review and refines the the output. Your task is to produce a polished final draft based on the text: {{$input}}. If this is the final draft, include the message 'TERMINATE' at the end.";

        // Define the task (interactive)
        Console.Write("Enter a task for the agent (press Enter to use a sample like: Gimme vacation idea for Hawaii for a week for 4 people including 2 children): ");
        var userInput = Console.ReadLine();
        var task = string.IsNullOrWhiteSpace(userInput)
            ? "Gimme vacation idea for Hawaii for a week for 4 people including 2 children"
            : userInput.Trim();

        // Create functions using CreateFunctionFromPrompt
        var TravelReporterFunction = kernel.CreateFunctionFromPrompt(TravelReporterPrompt);
        var TravelEditorFunction = kernel.CreateFunctionFromPrompt(TravelEditorPrompt);

        // Prepare the arguments
        var arguments = new KernelArguments();
        arguments["input"] = task;

        // Initial article draft by Travel Reporter
        var articleDraft = (await kernel.InvokeAsync(TravelReporterFunction, arguments)).ToString();
        Console.WriteLine($"\nInitial TravelReporter Output:\n{articleDraft}");

        // Iterative back and forth between Travel Reporter and Travel Editor
        int maxIterations = 5;
        for (int i = 0; i < maxIterations; i++)
        {
            // Pass the draft to the Travel Editor for rewriting
            arguments["input"] = articleDraft;
            var editorResult = (await kernel.InvokeAsync(TravelEditorFunction, arguments)).ToString();
            Console.WriteLine("==================");
            Console.WriteLine($"\nTravelEditor Rewritten Article (Iteration {i + 1}):\n{editorResult}");
            Console.WriteLine("==================");

            // Check for TERMINATE event
            if (editorResult.Contains("TERMINATE"))
            {
                Console.WriteLine("TERMINATE event detected. Ending iterations.");
                break;
            }

            // Pass the edited article back to the Travel Reporter for further refinement
            arguments["input"] = editorResult;
            var reporterResult = (await kernel.InvokeAsync(TravelReporterFunction, arguments)).ToString();
            Console.WriteLine($"\nTravelReporter Refined Article (Iteration {i + 1}):\n{reporterResult}");
            Console.WriteLine("==================");

            // Update the article draft for the next iteration
            articleDraft = reporterResult;
        }
        Console.WriteLine("========FINAL==========");
        Console.WriteLine($"\nFinalized Article:\n{articleDraft}");
        Console.WriteLine("\nTask completed. TERMINATE");

        Console.WriteLine("\nPress any key to close this window...");
        Console.ReadKey();
    }

}
