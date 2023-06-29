using GPTTest.Contracts;
using GPTTest.Handlers;
using GPTTest.Hubs;
using GPTTest.Models;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using OpenAI.Extensions;

var builder = WebApplication.CreateBuilder(args);

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
string DbPath = System.IO.Path.Join(path, "GPTTest.db");
//Create a DB for test purposes at %LocalAppData/GPTTest.db%
builder.Services.AddDbContext<GptTestContext>(opt =>
    opt.UseSqlite(@$"Data Source={DbPath}"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRazorPages();

builder.Services.AddOpenAIService();

builder.Services.AddSignalR();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<IChatGptHandler, ChatGptHandler>();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapControllers();
app.UseRouting();

app.MapRazorPages();

//map Hubs
app.MapHub<ChatHub>("/chatHub");


using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<GptTestContext>();
    
    // Here is the migration executed
    dbContext.Database.Migrate();
}

app.Run();