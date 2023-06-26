using GPTTest.Contracts;
using GPTTest.Handlers;
using GPTTest.Hubs;
using GPTTest.Models;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using OpenAI.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<GptTestContext>(opt =>
    opt.UseInMemoryDatabase("AppointmentTest"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRazorPages();

builder.Services.AddOpenAIService();

builder.Services.AddSignalR();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<IChatGptHandler, ChatGptHandler>();

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


app.Run();