using Api.Db;
using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddConsole();
builder.Services.AddFastEndpoints();
WebApplication app = builder.Build();
app.UseFastEndpoints();
using (ApplicationContext db = new(app.Configuration))
{
    db.Database.EnsureCreated();
}
app.Run();
