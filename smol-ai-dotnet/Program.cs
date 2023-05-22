using ChatGPT;
using System.Text.Json;

public class Program
{

    const string APIKEY = "===API_KEY===";
    const string generatedDir = "generated"; // generators folder of files, for me it's smol-ai-dotnet\bin\Debug\net7.0\generated
    const string openai_model = "gpt-3.5-turbo"; // or 'gpt-4'
    const int openai_model_max_tokens = 2048;

    public static async Task<string> GenerateResponse(string systemPrompt, string userPrompt, params string[] args)
    {
        // TODO: Fix encoding. Not important to get this working
        //var encoding = TikToken.EncodingForModel(openai_model);
        ReportTokens(systemPrompt);
        ReportTokens(userPrompt);

        var messages = new List<Message>();

        messages.Add(new Message { role = "system", content = systemPrompt });
        messages.Add(new Message { role = "user", content = userPrompt });

        // alternative between assistant and user for each arg
        var role = "assistant";
        foreach (var value in args)
        {
            messages.Add(new Message { role = role, content = value });
            ReportTokens(value);
            role = role == "assistant" ? "user" : "assistant";
        }

        var parameters = new ChatCompletionParams
        {
            model = openai_model,
            messages = messages,
            max_tokens = openai_model_max_tokens,
            temperature = 0
        };

        var client = new OpenAiClient(APIKEY);
        Response response = await client.CallChatCompletionAsync(parameters);

        if (response.error is Error error)
        {
            // response.Error "That model is currently overloaded with other requests. You can retry your request, or contact us through our help center at help.openai.com if the error persists. (Please include the request ID b24afc2c91294744679576584d2954b2 in your message.)"
            return error.message;
        }

        var reply = response.choices[0].message.content;
        return reply;
    }

    public static async Task<(string, string)> GenerateFile(string filename, string filepathsString = null, string sharedDependencies = null, string prompt = null)
    {
        var filecode = await GenerateResponse(
            // Assistant
            $@"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
            
            the app is: {prompt}

            the files we have decided to generate are: {filepathsString}

            the shared dependencies (like filenames and variable names) we have decided on are: {sharedDependencies}
            
            only write valid code for the given filepath and file type, and return only the code.
            do not add any other explanation, only return valid code for that file type.",

            // User
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

  

    private static void ReportTokens(string prompt)
    {
        Console.WriteLine($"\u001b[37m{prompt.Count()} tokens\u001b[0m in prompt: \u001b[92m{prompt.Substring(0, 50)}\u001b[0m");
    }

    private static void WriteFile(string filename, string filecode, string directory)
    {
        Console.WriteLine("\u001b[94m[" + filename + "]\u001b[0m");
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


    public static async Task MainAsync(string prompt, string directory = generatedDir, string? file = null)
    {
    
        if (prompt.EndsWith(".md"))
        {
            prompt = File.ReadAllText(prompt);
        }

        Console.WriteLine("hi its me, 🐣the smol developer🐣! you said you wanted:");
        Console.WriteLine("\u001b[92m" + prompt + "\u001b[0m");

        var filepathsString = await GenerateResponse(
            // Assistant content
            @"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
            
            When given their intent, create a complete, exhaustive list of filepaths that the user would write to make the program.
            
            only list the filepaths you would write, and return them without a leaing slash as a json list of strings. 
            do not add any other explanation, only return a json list of strings ",
            // User Content
            prompt
        );

        Console.WriteLine(filepathsString);

        var listActual = new List<string>();
        try
        {
            listActual = JsonSerializer.Deserialize<List<string>>(filepathsString);

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
                    // Assistant Content
                    @"You are an AI developer who is trying to write a program that will generate code for the user based on their intent.
                    
                    In response to the user's prompt:
            
                    ---
                    the app is: {prompt}
                    ---
                    
                    the files we have decided to generate are: {filepathsString}

                    Now that we have a list of files, we need to understand what dependencies they share.
                    Please name and briefly describe what is shared between the files we are generating, including _Layouts.cshtml files.
                    Exclusively focus on the names of the shared dependencies, and do not add any other explanation.",  
                    // User Content
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



    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide the initial prompt as the first argument.");
            return;
        }

        string prompt = args[0]; 
        string directory = "generated"; // Set the directory path here
        
        await MainAsync(prompt, directory);
    }


}
