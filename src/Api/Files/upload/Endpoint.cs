using Api.Db;
using FastEndpoints;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;


namespace Api.Files.Upload
{
    public class Endpoint(IConfiguration configuration, ILogger<Endpoint> logger) : EndpointWithoutRequest<Response>
    {

        public override void Configure()
        {
            Post("/api/files");
            AllowFileUploads();
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            if (Files.Count > 0)
            {
                var extensionProvider = new FileExtensionContentTypeProvider();
                using (ApplicationContext db = new(configuration))
                {
                    logger.LogDebug("Save {FilesCount} files", Files.Count());

                    foreach (var file in Files)
                    {
                        if (!extensionProvider.TryGetContentType(file.FileName, out string? contentType))
                        {
                            contentType = "application/octet-stream";
                        }
                        using (Aes aes = Aes.Create())
                        {
                            using (Stream uploadFileStream = file.OpenReadStream())
                            {
                                aes.GenerateKey();
                                aes.GenerateIV();
                                using var md5 = MD5.Create();

                                Db.File dbFile = new()
                                {
                                    Id = Guid.NewGuid().ToString("n"),
                                    ContentType = contentType,
                                    Name = file.FileName,
                                    Hash = BitConverter.ToString(md5.ComputeHash(uploadFileStream)).Replace("-", string.Empty),
                                    CreatedAt = DateTime.Now,
                                    Key = aes.Key,
                                    Vector = aes.IV,
                                    Length = file.Length
                                };
                                logger.LogDebug("Generate new id ({dbFile.Id}) for file {file.filename}", dbFile.Id, file.FileName);
                                
                                using (FileStream fsOut = new(Path.Combine(configuration.GetValue<string>("Files:SavePath", ".")!, dbFile.Id), FileMode.OpenOrCreate, FileAccess.Write))       //create a file stream for output
                                {
                                    using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                                    {
                                        using (CryptoStream cs = new(fsOut, encryptor, CryptoStreamMode.Write))
                                        {
                                            try
                                            {
                                                uploadFileStream.Seek(0, SeekOrigin.Begin);
                                                int bufferLen = 81920;
                                                byte[] buffer = new byte[bufferLen];

                                                int bytesRead;
                                                do
                                                {
                                                    bytesRead = uploadFileStream.Read(buffer, 0, bufferLen);     // read a chunk of data from the input file
                                                    await cs.WriteAsync(buffer.AsMemory(0, bytesRead), ct);      // write to cryptostream
                                                }
                                                while (bytesRead != 0);
                                                Array.Clear(buffer, 0, buffer.Length);
                                            }
                                            catch (Exception e)

                                            { throw new Exception("Error occurred while encrypting file: " + e.Message); }
                                        }
                                    }
                                }
                                await db.Files.AddAsync(dbFile, ct);
                            }
                        }
                    }
                    await db.SaveChangesAsync(ct);
                    await Send.OkAsync(new Response([.. db.Files.Select(file => new File(file.Name,file.Length,file.Id))]), ct);
                }
                return;
            }
            await Send.NoContentAsync(ct);
        }
    }
}
