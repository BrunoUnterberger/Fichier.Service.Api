namespace Api.Files.Upload
{
    public record Response(IEnumerable<File> Files);
    public record File(string Name, long Length, string Id);
}
