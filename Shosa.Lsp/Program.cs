using System.Text;
using Shosa.Lsp;

// The language server speaks JSON-RPC over stdin/stdout, framed the LSP way:
// "Content-Length: N\r\n\r\n" and then N bytes of UTF-8 JSON. Everything it knows is in
// LanguageServer; this file only moves bytes.

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
var writeLock = new object();

var server = new LanguageServer(message =>
{
    byte[] body = Encoding.UTF8.GetBytes(message);
    byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
    lock (writeLock)
    {
        output.Write(header);
        output.Write(body);
        output.Flush();
    }
});

while (!server.ExitRequested)
{
    int length = ReadHeader(input);
    if (length < 0)
        break;

    var body = new byte[length];
    int read = 0;
    while (read < length)
    {
        int n = input.Read(body, read, length - read);
        if (n <= 0)
            return 1;
        read += n;
    }

    try
    {
        server.Handle(Encoding.UTF8.GetString(body));
    }
    catch (Exception ex)
    {
        // One bad message must not end the session; the editor keeps typing.
        Console.Error.WriteLine($"shosa-lsp: {ex}");
    }
}

return 0;

static int ReadHeader(Stream stream)
{
    int contentLength = -1;
    var line = new StringBuilder();
    while (true)
    {
        int b = stream.ReadByte();
        if (b < 0)
            return -1;
        if (b == '\n')
        {
            string text = line.ToString().TrimEnd('\r');
            line.Clear();
            if (text.Length == 0)
                return contentLength;
            if (text.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(text["Content-Length:".Length..].Trim());
        }
        else
        {
            line.Append((char)b);
        }
    }
}
