namespace Api.Files.Upload
{
    public record Response(IList<File> Files);
    public record File(string Name, long Length, string Id, string Hash);
}
