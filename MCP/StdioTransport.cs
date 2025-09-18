using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SCOMMCPServer.MCP
{
    public class StdioTransport
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        public StdioTransport()
        {
            _input = Console.In;
            _output = Console.Out;
            _error = Console.Error;
            
            // Set encoding to UTF-8
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        }

        public async Task<JObject> ReadMessageAsync()
        {
            try
            {
                var line = await _input.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    return null;

                return JObject.Parse(line);
            }
            catch (Exception ex)
            {
                _error.WriteLine($"Error reading message: {ex.Message}");
                return null;
            }
        }

        public async Task WriteMessageAsync(JObject message)
        {
            try
            {
                var json = message.ToString(Newtonsoft.Json.Formatting.None);
                await _output.WriteLineAsync(json);
                await _output.FlushAsync();
            }
            catch (Exception ex)
            {
                _error.WriteLine($"Error writing message: {ex.Message}");
            }
        }
    }
}