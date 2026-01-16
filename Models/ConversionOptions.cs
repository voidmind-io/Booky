namespace Booky.Models;

public class ConversionOptions
{
    public required string Title { get; set; }
    public string? Author { get; set; }
    public required string InputPath { get; set; }
    public required string OutputPath { get; set; }
}
