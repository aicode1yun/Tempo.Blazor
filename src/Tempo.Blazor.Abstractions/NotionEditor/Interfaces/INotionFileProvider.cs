namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionFileProvider
{
    Task<string> UploadFileAsync(Stream content, string fileName, string contentType);
    Task<string> GetFileUrlAsync(string fileId);
    Task DeleteFileAsync(string fileId);
    Task<long> GetFileSizeAsync(string fileId);
}
