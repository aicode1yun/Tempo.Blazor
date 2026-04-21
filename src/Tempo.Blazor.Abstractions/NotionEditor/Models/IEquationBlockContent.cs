namespace Tempo.Blazor.NotionEditor.Models;

public interface IEquationBlockContent : IBlockContent
{
    string Expression { get; }
}
