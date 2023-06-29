using Microsoft.EntityFrameworkCore;

namespace GPTTest.Models;

public class GptTestContext : DbContext
{
    public GptTestContext(DbContextOptions<GptTestContext> options)
    :base (options)
    {
        
    }

    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<ChatHistory> ChatHistories { get; set; } = null!;

}