using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "SETTINGS_MANAGE")]
    public class ReportSchedulesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportGeneratorService _reportService;
        private readonly IEmailService _emailService;
        private readonly AuditService _auditService;

        public ReportSchedulesController(
            ApplicationDbContext context,
            IReportGeneratorService reportService,
            IEmailService emailService,
            AuditService auditService)
        {
            _context = context;
            _reportService = reportService;
            _emailService = emailService;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReportSchedule>>> GetReportSchedules()
        {
            return await _context.ReportSchedules
                .Where(s => !s.EstSupprime)
                .AsNoTracking()
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ReportSchedule>> GetReportSchedule(int id)
        {
            var schedule = await _context.ReportSchedules.FindAsync(id);
            if (schedule == null || schedule.EstSupprime) return NotFound();
            return schedule;
        }

        [HttpPost]
        public async Task<ActionResult<ReportSchedule>> PostReportSchedule(ReportSchedule schedule)
        {
            schedule.NextRunAt = CalculateNextRun(schedule.Frequency, DateTime.UtcNow);

            _context.ReportSchedules.Add(schedule);
            await _context.SaveChangesAsync();

            _auditService.LogAction(
                "REPORT_CREATE",
                $"Création planification de rapport : {schedule.ReportName}",
                User.Identity?.Name,
                resourceName: schedule.ReportName);

            return CreatedAtAction(nameof(GetReportSchedule), new { id = schedule.Id }, schedule);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutReportSchedule(int id, ReportSchedule schedule)
        {
            if (id != schedule.Id) return BadRequest();

            var existing = await _context.ReportSchedules.FindAsync(id);
            if (existing == null || existing.EstSupprime) return NotFound();

            if (existing.Frequency != schedule.Frequency)
            {
                existing.NextRunAt = CalculateNextRun(schedule.Frequency, DateTime.UtcNow);
            }

            existing.ReportName = schedule.ReportName;
            existing.SelectedMetricsJson = schedule.SelectedMetricsJson;
            existing.ScopeFilterJson = schedule.ScopeFilterJson;
            existing.Frequency = schedule.Frequency;
            existing.EmailRecipients = schedule.EmailRecipients;
            existing.Format = schedule.Format;
            existing.DateModification = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _auditService.LogAction(
                "REPORT_UPDATE",
                $"Mise à jour planification de rapport : {schedule.ReportName}",
                User.Identity?.Name,
                resourceName: schedule.ReportName);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReportSchedule(int id)
        {
            var schedule = await _context.ReportSchedules.FindAsync(id);
            if (schedule == null || schedule.EstSupprime) return NotFound();

            schedule.EstSupprime = true;
            schedule.DateModification = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _auditService.LogAction(
                "REPORT_DELETE",
                $"Suppression planification de rapport : {schedule.ReportName}",
                User.Identity?.Name,
                resourceName: schedule.ReportName);

            return NoContent();
        }

        [HttpPost("{id}/send")]
        public async Task<IActionResult> SendReportImmediately(int id)
        {
            var schedule = await _context.ReportSchedules.FindAsync(id);
            if (schedule == null || schedule.EstSupprime) return NotFound();

            try
            {
                byte[] fileBytes;
                string fileName;
                string contentType;

                if (schedule.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
                {
                    fileBytes = await _reportService.GenerateCsvReportAsync(schedule);
                    fileName = $"Rapport_{schedule.ReportName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
                    contentType = "text/csv";
                }
                else
                {
                    fileBytes = await _reportService.GeneratePdfReportAsync(schedule);
                    fileName = $"Rapport_{schedule.ReportName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                    contentType = "application/pdf";
                }

                var recipients = schedule.EmailRecipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var recipient in recipients)
                {
                    var email = recipient.Trim();
                    if (string.IsNullOrEmpty(email)) continue;

                    await _emailService.SendEmailWithAttachmentAsync(
                        email,
                        $"[Autoprint] [Manuel] Rapport d'activité : {schedule.ReportName}",
                        $"<p>Bonjour,</p><p>Veuillez trouver ci-joint le rapport d'activité <strong>{schedule.ReportName}</strong> envoyé manuellement depuis la console Autoprint.</p><p>Cordialement,<br/>L'équipe Autoprint</p>",
                        fileBytes,
                        fileName,
                        contentType
                    );
                }

                _auditService.LogAction(
                    "REPORT_SEND_MANUAL",
                    $"Envoi manuel du rapport : {schedule.ReportName}",
                    User.Identity?.Name,
                    resourceName: schedule.ReportName);

                return Ok(new { message = "Rapport envoyé avec succès." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private DateTime CalculateNextRun(string frequency, DateTime baseTime)
        {
            var localToday = DateTime.Today;
            var targetHour = 6;

            if (frequency.Equals("Quotidien", StringComparison.OrdinalIgnoreCase))
            {
                return localToday.AddDays(1).AddHours(targetHour).ToUniversalTime();
            }
            else if (frequency.Equals("Hebdomadaire", StringComparison.OrdinalIgnoreCase))
            {
                int daysToAdd = ((int)DayOfWeek.Monday - (int)localToday.DayOfWeek + 7) % 7;
                if (daysToAdd == 0) daysToAdd = 7;
                return localToday.AddDays(daysToAdd).AddHours(targetHour).ToUniversalTime();
            }
            else if (frequency.Equals("Mensuel", StringComparison.OrdinalIgnoreCase))
            {
                var nextMonth = localToday.AddMonths(1);
                var firstDay = new DateTime(nextMonth.Year, nextMonth.Month, 1, targetHour, 0, 0);
                return firstDay.ToUniversalTime();
            }

            return baseTime.AddDays(1);
        }
    }
}
