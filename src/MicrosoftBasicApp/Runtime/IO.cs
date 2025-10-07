using System.Text;

namespace MicrosoftBasicApp.Runtime;

public interface IBasicIO
{
    string? ReadLine();
    void Write(string text);
    void WriteLine(string text = "");
}

public sealed class ConsoleBasicIO : IBasicIO
{
    public string? ReadLine() => Console.ReadLine();
    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text = "") => Console.WriteLine(text);
}

/// <summary>
/// Deterministic I/O channel primarily for automated tests.
/// </summary>
public sealed class BufferedBasicIO : IBasicIO
{
    private readonly Queue<string> _input;
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _lines = new();

    public BufferedBasicIO(IEnumerable<string>? input = null)
    {
        _input = new Queue<string>(input ?? Array.Empty<string>());
    }

    public string? ReadLine()
    {
        if (_input.Count == 0)
        {
            return null;
        }

        return _input.Dequeue();
    }

    public void Write(string text) => _buffer.Append(text);

    public void WriteLine(string text = "")
    {
        _buffer.AppendLine(text);
        _lines.Add(text);
    }

    public string GetBuffer() => _buffer.ToString();

    public IReadOnlyList<string> Lines => _lines;    
}
