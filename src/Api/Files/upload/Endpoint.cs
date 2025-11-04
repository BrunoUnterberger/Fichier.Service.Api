using FastEndpoints;


namespace Api.Files.upload
{
    public class Endpoint(IConfiguration config, ILogger<Endpoint> logger) : EndpointWithoutRequest<Response>
    {

        public override void Configure()
        {
            Post("/api/uploads");
            AllowFileUploads();
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            if (Files.Count > 0)
            {
                logger.LogDebug("Save {FilesCount} files", Files.Count());
                List<File> files = [];
                foreach (var file in Files)
                {
                    string id = Guid.NewGuid().ToString("n");
                    logger.LogDebug("Generate new id ({id}) for file {file.filename}",id, file.FileName);
                    using (var fileStream = System.IO.File.Create(Path.Combine(config.GetValue<string>("Files:SavePath", ".")!, id)))
                    {
                      file.OpenReadStream().CopyTo(fileStream);
                    }
                    logger.LogDebug("Save file {file.filename} ok", file.FileName);
                    files.Add(new File(file.FileName, file.Length, id));
                }
                await Send.OkAsync(new Response(files), ct);
                return;
            }

            await Send.NoContentAsync(ct);
        }
    }
}
