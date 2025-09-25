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
        
        // Define the agent prompts with input placeholders
        //var TravelReporterPrompt = "You are a helpful AI assistant who finds discounted travel deals: {{$input}}. Check known travel sites and Keep your output concise.For hotels, provide addresses, contacts and average listed prices. For flights, name the primary airlines and average fare. For destination attraction, list their addresses, contacts and hours";
        //var TravelEditorPrompt = "You are a helpful AI assistant that review and refines the the output. Your task is to produce a polished final draft based on the text: {{$input}}. If this is the final draft, include the message 'TERMINATE' at the end.";
        var TravelReporterPrompt =
@"You are a helpful AI assistant who finds discounted travel deals.
Use the conversation history to keep context and avoid asking for details already provided.

History:
{{$history}}

Task:
{{$input}}

Rules:
- Keep output concise.
- For hotels: include addresses, contacts, average listed prices.
- For flights: include primary airlines and average fare.
- For attractions: include addresses, contacts, and hours.";

        var TravelEditorPrompt =
@"You are a helpful AI assistant that reviews and refines the output.
Use the conversation history to keep context consistent.

History:
{{$history}}

Draft to refine:
{{$input}}

Produce a polished final draft. If the draft is final, include the message 'TERMINATE' at the end.";


        // Running conversation history across user turns
        var runningHistory = "";

        // Define the task (interactive)
        while (true)
        {
            Console.Write("Type 'exit' to quit, 'clear' to clear screen: ");
            Console.Write("Enter a task for the agent (press Enter to use a sample like: Gimme vacation idea for Hawaii for a week for 4 people including 2 children): ");

            var userInput = Console.ReadLine();
            if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(userInput, "clear", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userInput, "cls", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                try
                {
                    Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
                }
                catch
                {
                    //ignore for now
                }
                //clears the console buffer.
                Console.Write("\x1b[3J\x1b[H\x1b[2J");
                continue;
            }

            var task = string.IsNullOrWhiteSpace(userInput)
                ? "Gimme vacation idea for Hawaii for a week for 4 people including 2 children"
                : userInput.Trim();

            // Create functions using CreateFunctionFromPrompt
            var TravelReporterFunction = kernel.CreateFunctionFromPrompt(TravelReporterPrompt);
            var TravelEditorFunction = kernel.CreateFunctionFromPrompt(TravelEditorPrompt);

            // Prepare the arguments
            var arguments = new KernelArguments()
            {
                ["history"] = runningHistory
            };

            // Initial article draft by Travel Reporter
            arguments["input"] = task;
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

            //Console.WriteLine("\nPress any key to close this window...");
            //Console.ReadKey();

            // Clean and append this turn into history
            var finalForHistory = articleDraft.Replace("TERMINATE", "").Trim();
            runningHistory +=
                            $@"User: {task}
                            Assistant: {finalForHistory}
                            ";

            // Simple guard to keep context small
            if (runningHistory.Length > 8000)
            {
                runningHistory = runningHistory[^8000..];
            }

        }
    }
}
