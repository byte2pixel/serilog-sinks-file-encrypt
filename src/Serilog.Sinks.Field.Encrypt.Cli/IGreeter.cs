namespace Serilog.Sinks.Field.Encrypt.Cli;

public interface IGreeter
{
    void Greet(string name);
}

public sealed class HelloWorldGreeter : IGreeter
{
    public void Greet(string name)
    {
        Console.WriteLine($"Hello, {name}!");
    }
}
