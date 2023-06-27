namespace GPTTest.Models;

public class Appointment
{
    public long Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int NumberOfPeople { get; set; }
}