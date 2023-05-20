public class Program
{

    const string generatedDir = "generated";

    const string openai_model = "gpt-4"; // or 'gpt-3.5-turbo'
    const int openai_model_max_tokens = 2000;


    public static async Task<string> GenerateResponse(string systemPrompt, string userPrompt, params string[] args)
    {
        //var encoding = TikToken.EncodingForModel(openai_model);
        ReportTokens(systemPrompt);
        ReportTokens(userPrompt);

        var messages = new List<Message>();
        messages.Add(new Message { Role = "system", Content = systemPrompt });
        messages.Add(new Message { Role = "user", Content = userPrompt });

        var role = "assistant";
        foreach (var value in args)
        {
            messages.Add(new Message { Role = role, Content = value });
            ReportTokens(value);
            role = role == "assistant" ? "user" : "assistant";
        }

        var parameters = new ChatCompletionParams
        {
            Model = openai_model,
            Messages = messages,
            MaxTokens = openai_model_max_tokens,
            Temperature = 0
        };

        // var response = await CallChatCompletionAsync(parameters);

        //var reply = response.Choices[0].Message.Content;
        //return reply;
        return "reply";
    }



    public static async Task<(string, string)> GenerateFile(string filename, string filepathsString = null, string sharedDependencies = null, string prompt = null)
    {
        var filecode = await GenerateResponse(
            $@"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
            
            the app is: {prompt}

            the files we have decided to generate are: {filepathsString}

            the shared dependencies (like filenames and variable names) we have decided on are: {sharedDependencies}
            
            only write valid code for the given filepath and file type, and return only the code.
            do not add any other explanation, only return valid code for that file type.",
            $@"We have broken up the program into per-file generation. 
            Now your job is to generate only the code for the file {filename}. 
            Make sure to have consistent filenames if you reference other files we are also generating.
            
            Remember that you must obey 3 things: 
               - you are generating code for the file {filename}
               - do not stray from the names of the files and the shared dependencies we have decided on
               - MOST IMPORTANT OF ALL - the purpose of our app is {prompt} - every line of code you generate must be valid code. Do not include code fences in your response, for example
            
            Bad response:
            ```javascript 
            console.log(""hello world"")
            ```
            
            Good response:
            console.log(""hello world"")
            
            Begin generating the code now."
        );

        return (filename, filecode);
    }

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the initial prompt as the first argument.");
            return;
        }

        string prompt = args[0]; // Set your prompt here        
        string directory = "generated"; // Set the directory path here
        string file = null; // Set the file name here or leave it as null

        // Your existing code here

        await MainAsync(prompt, directory, file);
    }


    public static async Task MainAsync(string prompt, string directory = generatedDir, string file = null)
    {
        if (prompt.EndsWith(".md"))
        {
            prompt = File.ReadAllText(prompt);
        }

        Console.WriteLine("hi its me, 🐣the smol developer🐣! you said you wanted:");
        Console.WriteLine("\u001b[92m" + prompt + "\u001b[0m");

        var filepathsString = await GenerateResponse(
            @"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
            
            When given their intent, create a complete, exhaustive list of filepaths that the user would write to make the program.
            
            only list the filepaths you would write, and return them as a python list of strings. 
            do not add any other explanation, only return a python list of strings.",
            prompt
        );

        Console.WriteLine(filepathsString);

        var listActual = new List<string>();
        try
        {
            listActual = filepathsString.Split(new[] { ',', '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            string sharedDependencies = null;
            if (File.Exists("shared_dependencies.md"))
            {
                sharedDependencies = File.ReadAllText("shared_dependencies.md");
            }

            if (file != null)
            {
                Console.WriteLine("file" + file);
                var (filename, filecode) = await GenerateFile(file, filepathsString, sharedDependencies, prompt);
                WriteFile(filename, filecode, directory);
            }
            else
            {
                CleanDirectory(directory);

                sharedDependencies = await GenerateResponse(
                    @"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
                    
                    In response to the user's prompt:
            
                    ---
                    the app is: {prompt}
                    ---
                    
                    the files we have decided to generate are: {filepathsString}

                    Now that we have a list of files, we need to understand what dependencies they share.
                    Please name and briefly describe what is shared between the files we are generating, including exported variables, data schemas, id names of every DOM elements that javascript functions will use, message names, and function names.
                    Exclusively focus on the names of the shared dependencies, and do not add any other explanation.",
                    prompt
                );

                Console.WriteLine(sharedDependencies);
                WriteFile("shared_dependencies.md", sharedDependencies, directory);

                foreach (var f in listActual)
                {
                    var (filename, filecode) = await GenerateFile(f, filepathsString, sharedDependencies, prompt);
                    WriteFile(filename, filecode, directory);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to parse result: " + ex.Message);
        }
    }

    private static void ReportTokens(string prompt)
    {
        Console.WriteLine($"\u001b[37m{prompt.Count()} tokens\u001b[0m in prompt: \u001b[92m{prompt.Substring(0, 50)}\u001b[0m");
    }

    private static void WriteFile(string filename, string filecode, string directory)
    {
        Console.WriteLine("\u001b[94m" + filename + "\u001b[0m");
        Console.WriteLine(filecode);

        var filePath = Path.Combine(directory, filename);
        var dir = Path.GetDirectoryName(filePath);
        Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, filecode);
    }

    private static void CleanDirectory(string directory)
    {
        var extensionsToSkip = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".ico", ".tif", ".tiff" };

        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var extension = Path.GetExtension(file);
                if (!extensionsToSkip.Contains(extension))
                {
                    File.Delete(file);
                }
            }
        }
        else
        {
            Directory.CreateDirectory(directory);
        }
    }
}
