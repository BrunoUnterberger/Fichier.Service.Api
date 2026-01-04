using Api.Db;
using FastEndpoints;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Cryptography;

namespace Api.Files.Upload
{
    public class Endpoint(IConfiguration configuration, ILogger<Endpoint> logger) : Endpoint<Request,Response>
    {
        public override void Configure()
        {
            Post("/api/files");
            AllowFileUploads();
            AllowAnonymous();
        }

        public override async Task HandleAsync(Request request, CancellationToken ct)
        {
            if (Files.Count == 0)
            {
                await Send.NoContentAsync(ct);
                return;
            }

            var savePath = configuration.GetValue<string>("Files:SavePath", ".")!;
            EnsureSaveDirectory(savePath);

            var extensionProvider = new FileExtensionContentTypeProvider();
            var uploadFiles = new List<Db.File>();

            using var db = new ApplicationContext(configuration);
            logger.LogDebug("Save {FilesCount} files", Files.Count);

            foreach (var file in Files)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var dbFile = await ProcessFileAsync(file, savePath, extensionProvider, ct);
                    logger.LogDebug("Generated id {FileId} for file {FileName}", dbFile.Id, dbFile.Name);
                    uploadFiles.Add(dbFile);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("File upload cancelled for {FileName}", file.FileName);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while processing upload for {FileName}", file.FileName);
                    throw;
                }
            }

            if (uploadFiles.Count > 0)
            {
                await db.Files.AddRangeAsync(uploadFiles, ct);
                await db.SaveChangesAsync(ct);

                Response result = new([.. uploadFiles.Select(f => new File(f.Name,f.Length,f.Id,f.Hash))]);
                await Send.OkAsync(result, ct);
                return;
            }

            await Send.NoContentAsync(ct);
        }

        private void EnsureSaveDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to create save directory: {Path}", path);
                throw;
            }
        }

        private async Task<Db.File> ProcessFileAsync(Microsoft.AspNetCore.Http.IFormFile file, string savePath, FileExtensionContentTypeProvider extensionProvider, CancellationToken ct)
        {
            if (!extensionProvider.TryGetContentType(file.FileName, out string? contentType))
            {
                contentType = "application/octet-stream";
            }

            var fileId = Guid.NewGuid().ToString("n");
            var outPath = Path.Combine(savePath, fileId);

            try
            {
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();

                using var uploadFileStream = file.OpenReadStream();
                using var fsOut = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var cs = new CryptoStream(fsOut, encryptor, CryptoStreamMode.Write);
                using var md5 = MD5.Create();

                int bufferLen = 81920;
                byte[] buffer = new byte[bufferLen];
                int bytesRead;
                while ((bytesRead = await uploadFileStream.ReadAsync(buffer.AsMemory(0, bufferLen), ct)) > 0)
                {
                    // update MD5 and write encrypted chunk
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    await cs.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                }
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                // finalize crypto stream and flush to disk
                cs.FlushFinalBlock();
                await fsOut.FlushAsync(ct);

                var hash = BitConverter.ToString(md5.Hash!).Replace("-", string.Empty);

                var dbFile = new Db.File
                {
                    Id = fileId,
                    ContentType = contentType,
                    Name = file.FileName,
                    Hash = hash,
                    CreatedAt = DateTime.UtcNow,
                    Key = aes.Key,
                    Vector = aes.IV,
                    Length = file.Length
                };

                return dbFile;
            }
            catch (OperationCanceledException)
            {
                // remove partial file when cancellation occurs
                await DeleteFileIfExists(outPath);
                throw;
            }
            catch
            {
                // remove partial file on any error and rethrow
                await DeleteFileIfExists(outPath);
                throw;
            }
        }

        private Task DeleteFileIfExists(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete incomplete file {Path}", path);
            }
            return Task.CompletedTask;
        }
    }
}
