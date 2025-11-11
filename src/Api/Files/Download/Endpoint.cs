using FastEndpoints;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;


namespace Api.Files.Download
{
    public class Endpoint(IConfiguration config, ILogger<Endpoint> logger) : EndpointWithoutRequest
    {

        public override void Configure()
        {
            Get("/api/files/{id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            string? id = Route<string>("id", isRequired: true);
            if (string.IsNullOrWhiteSpace(id))
            {
                await Send.NotFoundAsync(ct);
                return;
            }
            string filePath = Path.Combine(config.GetValue<string>("Files:SavePath", ".")!, id);
            if (File.Exists(filePath))
            {
                // Extraction des 100000 derniers caractères
                using (FileStream file = new(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (file.Length > 20)
                    {
                        file.Seek(-20, SeekOrigin.End);
                    }
                    byte[] s = new byte[20];
                    await file.ReadAtLeastAsync(s, 20, true, ct);
                    int tailleFlux = int.Parse(Encoding.UTF8.GetString(s.Where(b => b != 0).ToArray()));
                    file.Seek(tailleFlux, SeekOrigin.Begin);
                    byte[] e = new byte[file.Length - tailleFlux - 20];
                    await file.ReadAtLeastAsync(e, (int)(file.Length - tailleFlux - 20), true, ct);
                    Dictionary<string, object>? donnees = JsonSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(e.Where(b => b != 0).ToArray()));
                    //  Dictionary<string, object>? x = JsonSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(dataFile));

                }
                   


                //Dictionary<string, string>?  data  = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(extensionFilePath,ct));
                //await Send.FileAsync(new FileInfo(filePath), data!["contentType"], DateTime.Parse(data!["createdDate"]),false,ct);
                //return;
            }
            await Send.NoContentAsync(ct);
        }
    }
}
