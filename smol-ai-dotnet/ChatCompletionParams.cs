internal class ChatCompletionParams
{
    public string Model { get; set; }
    public List<Message> Messages { get; set; }
    public int MaxTokens { get; set; }
    public int Temperature { get; set; }
}