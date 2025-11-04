namespace Api.Files.upload
{
    public record Response(IEnumerable<File> Files);
    public record File(string Name, long Length, string Id);
}
