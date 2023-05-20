
namespace ChatGPT
{
    public class ChatCompletionParams
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public int max_tokens { get; set; }

        public int temperature { get; set; }
    } 
}