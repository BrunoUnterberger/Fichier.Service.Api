using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Net.Mime;

namespace Api.Db;
public class File
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required long Length { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string Hash { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Key { get; set; }
    public required byte[] Vector { get; set; }
}
