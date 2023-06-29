using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GPTTest.Models;

namespace GPTTest.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly GptTestContext _context;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(GptTestContext context, ILogger<AppointmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Appointment
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Appointment>>> GetAppointments()
        {
          if (_context.Appointments == null)
          {
              return NotFound();
          }
            return await _context.Appointments.ToListAsync();
        }

        // GET: api/Appointment/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Appointment>> GetAppointment(long id)
        {
          if (_context.Appointments == null)
          {
              return NotFound();
          }
            var appointment = await _context.Appointments.FindAsync(id);

            if (appointment == null)
            {
                return NotFound();
            }

            return appointment;
        }

        // PUT: api/Appointment/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAppointment(long id, Appointment appointment)
        {
            if (id != appointment.Id)
            {
                return BadRequest();
            }

            _context.Entry(appointment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppointmentExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Appointment
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Appointment>> PostAppointment(Appointment appointment)
        {
          if (_context.Appointments is null)
          {
              return Problem("Entity set 'GptTestContext.Appointments'  is null.");
          }
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAppointment), new { id = appointment.Id }, appointment);
        }

        // DELETE: api/Appointment/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAppointment(long id)
        {
            if (_context.Appointments == null)
            {
                return NotFound();
            }
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AppointmentExists(long id)
        {
            return (_context.Appointments?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        [HttpGet("AppointmentAvailable")]
        public async Task<IActionResult>? AppointmentSlotAvailable(DateTime date)
        {
            _logger.LogInformation("Checking Appointment availability for" + date.ToString("R"));
            bool available = !(_context.Appointments?.Any(e => e.Date.Date == date.Date && e.Date.Hour == date.Hour))
                .GetValueOrDefault();
            DateTime nextAvailableTime = date; 
            if (!available)
            {
                bool next = false;
                while (!next)
                {
                    nextAvailableTime = nextAvailableTime.AddHours(1);
                    next = !(_context.Appointments?.Any(e => e.Date.Date == nextAvailableTime.Date && e.Date.Hour == nextAvailableTime.Hour))
                        .GetValueOrDefault();
                }
            }
            _logger.LogInformation("Available: " + available);
            
            return Ok(new { isAvailable = available, nextAvailableTime = nextAvailableTime.ToString("R") });
        }
    }
}
