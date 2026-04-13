using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FeedbackAPI.Data;
using FeedbackAPI.Models;
using System.Text.RegularExpressions;

namespace FeedbackAPI.Controllers
{
    [ApiController]
    [Route("api/feedback")]
    public class FeedbackController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FeedbackController> _logger;

        private static readonly string[] AllowedCategories =
            { "General", "Venue", "Registration", "Staff", "Food", "Technical", "Other" };

        public FeedbackController(AppDbContext db, ILogger<FeedbackController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private string GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private async Task WriteAudit(string action, string entity, int? entityId,
            string? details, bool success, string? error = null)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Action       = action,
                    Entity       = entity,
                    EntityId     = entityId,
                    Details      = details,
                    IpAddress    = GetClientIp(),
                    Success      = success,
                    ErrorMessage = error,
                    Created_at   = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AUDIT | ACTION={Action} | STATUS=LOG_WRITE_FAILED | MESSAGE={Msg}", action, ex.Message);
            }
        }

        // GET api/feedback/events
        [HttpGet("events")]
        public async Task<IActionResult> GetEvents()
        {
            _logger.LogInformation("AUDIT | ACTION=GET_EVENTS | STATUS=REQUEST | IP={Ip}", GetClientIp());
            try
            {
                var events = await _db.Events
                    .Where(e => e.Event_Status == "active")
                    .Select(e => new
                    {
                        e.Id,
                        e.Event_Name,
                        e.Event_Type,
                        e.Event_Status,
                        e.Event_Date
                    })
                    .ToListAsync();

                _logger.LogInformation("AUDIT | ACTION=GET_EVENTS | STATUS=SUCCESS | COUNT={Count} | IP={Ip}", events.Count, GetClientIp());
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_EVENTS | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("GET_EVENTS", "Event", null, null, false, ex.Message);
                return StatusCode(500, "An error occurred while fetching events.");
            }
        }

        // POST api/feedback/submit
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] Feedback feedback)
        {
            _logger.LogInformation("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=REQUEST | EVENT={EventId} | IP={Ip}", feedback?.Event_id, GetClientIp());

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                _logger.LogWarning("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=VALIDATION_FAILED | ERRORS={Errors} | IP={Ip}", string.Join(", ", errors), GetClientIp());
                await WriteAudit("SUBMIT_FEEDBACK", "Feedback", null, $"Validation failed: {string.Join(", ", errors)}", false);
                return BadRequest(new { errors });
            }

            if (!AllowedCategories.Contains(feedback.Category))
            {
                _logger.LogWarning("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=INVALID_CATEGORY | VALUE={Category} | IP={Ip}", feedback.Category, GetClientIp());
                await WriteAudit("SUBMIT_FEEDBACK", "Feedback", null, $"Invalid category: {feedback.Category}", false);
                return BadRequest(new { errors = new[] { "Invalid category selected." } });
            }

            feedback.Message = Regex.Replace(feedback.Message, "<.*?>", string.Empty).Trim();

            if (feedback.Message.Length > 1000)
            {
                _logger.LogWarning("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=MESSAGE_TOO_LONG | LENGTH={Len} | IP={Ip}", feedback.Message.Length, GetClientIp());
                return BadRequest(new { errors = new[] { "Message cannot exceed 1000 characters." } });
            }

            try
            {
                var ev = await _db.Events.FindAsync(feedback.Event_id);
                if (ev == null)
                {
                    _logger.LogWarning("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=EVENT_NOT_FOUND | EVENT={EventId} | IP={Ip}", feedback.Event_id, GetClientIp());
                    await WriteAudit("SUBMIT_FEEDBACK", "Feedback", null, $"Event {feedback.Event_id} not found", false);
                    return NotFound(new { errors = new[] { "Event not found." } });
                }

                feedback.Status     = "Pending";
                feedback.Created_at = DateTime.UtcNow;

                _db.Feedbacks.Add(feedback);
                await _db.SaveChangesAsync();

                _logger.LogInformation("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=SUCCESS | FEEDBACK_ID={FeedbackId} | EVENT={EventId} | RATING={Rating} | CATEGORY={Category} | ANONYMOUS={Anon} | IP={Ip}",
                    feedback.Id, feedback.Event_id, feedback.Rating, feedback.Category, feedback.Is_Anonymous, GetClientIp());

                await WriteAudit("SUBMIT_FEEDBACK", "Feedback", feedback.Id,
                    $"Event {feedback.Event_id}, Rating {feedback.Rating}, Category {feedback.Category}", true);

                return Ok(new { feedback.Id, message = "Feedback submitted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=SUBMIT_FEEDBACK | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("SUBMIT_FEEDBACK", "Feedback", null, null, false, ex.Message);
                return StatusCode(500, "An error occurred while submitting feedback.");
            }
        }

        // GET api/feedback/status/{id}
        [HttpGet("status/{id}")]
        public async Task<IActionResult> GetStatus(int id)
        {
            _logger.LogInformation("AUDIT | ACTION=GET_STATUS | STATUS=REQUEST | FEEDBACK_ID={Id} | IP={Ip}", id, GetClientIp());

            if (id <= 0)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_STATUS | STATUS=INVALID_ID | VALUE={Id} | IP={Ip}", id, GetClientIp());
                return BadRequest(new { errors = new[] { "Invalid feedback ID." } });
            }

            try
            {
                var feedback = await _db.Feedbacks.FindAsync(id);
                if (feedback == null)
                {
                    _logger.LogWarning("AUDIT | ACTION=GET_STATUS | STATUS=NOT_FOUND | FEEDBACK_ID={Id} | IP={Ip}", id, GetClientIp());
                    return NotFound(new { errors = new[] { "Feedback not found." } });
                }

                _logger.LogInformation("AUDIT | ACTION=GET_STATUS | STATUS=SUCCESS | FEEDBACK_ID={Id} | RESULT={Status} | IP={Ip}", id, feedback.Status, GetClientIp());
                await WriteAudit("GET_STATUS", "Feedback", id, null, true);
                return Ok(new
                {
                    feedback.Id,
                    feedback.Status,
                    feedback.Rating,
                    feedback.Category,
                    feedback.Created_at
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_STATUS | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("GET_STATUS", "Feedback", id, null, false, ex.Message);
                return StatusCode(500, "An error occurred while fetching feedback status.");
            }
        }

        // GET api/feedback/event/{eventId}
        [HttpGet("event/{eventId}")]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            _logger.LogInformation("AUDIT | ACTION=GET_EVENT_FEEDBACK | STATUS=REQUEST | EVENT={EventId} | IP={Ip}", eventId, GetClientIp());

            if (eventId <= 0)
                return BadRequest(new { errors = new[] { "Invalid event ID." } });

            try
            {
                var feedbacks = await _db.Feedbacks
                    .Where(f => f.Event_id == eventId)
                    .Select(f => new
                    {
                        f.Id,
                        f.Rating,
                        f.Category,
                        f.Message,
                        f.Status,
                        f.Is_Anonymous,
                        f.Created_at
                    })
                    .OrderByDescending(f => f.Created_at)
                    .ToListAsync();

                _logger.LogInformation("AUDIT | ACTION=GET_EVENT_FEEDBACK | STATUS=SUCCESS | EVENT={EventId} | COUNT={Count} | IP={Ip}", eventId, feedbacks.Count, GetClientIp());
                await WriteAudit("GET_EVENT_FEEDBACK", "Feedback", eventId, $"Returned {feedbacks.Count} records", true);
                return Ok(feedbacks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_EVENT_FEEDBACK | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("GET_EVENT_FEEDBACK", "Feedback", eventId, null, false, ex.Message);
                return StatusCode(500, "An error occurred while fetching feedback.");
            }
        }

        // PATCH api/feedback/{id}/status
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
        {
            _logger.LogInformation("AUDIT | ACTION=UPDATE_STATUS | STATUS=REQUEST | FEEDBACK_ID={Id} | NEW_STATUS={Status} | IP={Ip}", id, status, GetClientIp());

            if (id <= 0)
                return BadRequest(new { errors = new[] { "Invalid feedback ID." } });

            var allowed = new[] { "Pending", "Reviewed" };
            if (string.IsNullOrWhiteSpace(status) || !allowed.Contains(status))
            {
                _logger.LogWarning("AUDIT | ACTION=UPDATE_STATUS | STATUS=INVALID_VALUE | VALUE={Status} | IP={Ip}", status, GetClientIp());
                return BadRequest(new { errors = new[] { "Status must be Pending or Reviewed." } });
            }

            try
            {
                var feedback = await _db.Feedbacks.FindAsync(id);
                if (feedback == null)
                {
                    _logger.LogWarning("AUDIT | ACTION=UPDATE_STATUS | STATUS=NOT_FOUND | FEEDBACK_ID={Id} | IP={Ip}", id, GetClientIp());
                    return NotFound(new { errors = new[] { "Feedback not found." } });
                }

                var oldStatus   = feedback.Status;
                feedback.Status = status;
                await _db.SaveChangesAsync();

                _logger.LogInformation("AUDIT | ACTION=UPDATE_STATUS | STATUS=SUCCESS | FEEDBACK_ID={Id} | FROM={Old} | TO={New} | IP={Ip}", id, oldStatus, status, GetClientIp());
                await WriteAudit("UPDATE_STATUS", "Feedback", id, $"Status changed from {oldStatus} to {status}", true);
                return Ok(new { message = "Status updated." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=UPDATE_STATUS | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("UPDATE_STATUS", "Feedback", id, null, false, ex.Message);
                return StatusCode(500, "An error occurred while updating status.");
            }
        }

        // GET api/feedback/analytics/{eventId}
        [HttpGet("analytics/{eventId}")]
        public async Task<IActionResult> Analytics(int eventId)
        {
            _logger.LogInformation("AUDIT | ACTION=GET_ANALYTICS | STATUS=REQUEST | EVENT={EventId} | IP={Ip}", eventId, GetClientIp());

            if (eventId <= 0)
                return BadRequest(new { errors = new[] { "Invalid event ID." } });

            try
            {
                var feedbacks = await _db.Feedbacks
                    .Where(f => f.Event_id == eventId)
                    .ToListAsync();

                if (!feedbacks.Any())
                    return Ok(new { total = 0, averageRating = 0,
                        byCategory = new object[0], byRating = new object[0] });

                var result = new
                {
                    total         = feedbacks.Count,
                    averageRating = Math.Round(feedbacks.Average(f => f.Rating), 2),
                    pending       = feedbacks.Count(f => f.Status == "Pending"),
                    reviewed      = feedbacks.Count(f => f.Status == "Reviewed"),
                    byCategory    = feedbacks
                        .GroupBy(f => f.Category)
                        .Select(g => new { category = g.Key, count = g.Count() })
                        .ToList(),
                    byRating      = feedbacks
                        .GroupBy(f => f.Rating)
                        .Select(g => new { rating = g.Key, count = g.Count() })
                        .OrderBy(x => x.rating)
                        .ToList()
                };

                _logger.LogInformation("AUDIT | ACTION=GET_ANALYTICS | STATUS=SUCCESS | EVENT={EventId} | TOTAL={Total} | AVG_RATING={Avg} | IP={Ip}", eventId, result.total, result.averageRating, GetClientIp());
                await WriteAudit("GET_ANALYTICS", "Feedback", eventId, $"Total: {result.total}, Avg: {result.averageRating}", true);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_ANALYTICS | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                await WriteAudit("GET_ANALYTICS", "Feedback", eventId, null, false, ex.Message);
                return StatusCode(500, "An error occurred while fetching analytics.");
            }
        }

        // GET api/feedback/audit-logs
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? action = null,
            [FromQuery] int page       = 1,
            [FromQuery] int pageSize   = 20)
        {
            _logger.LogInformation("AUDIT | ACTION=GET_AUDIT_LOGS | STATUS=REQUEST | IP={Ip}", GetClientIp());

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            try
            {
                var query = _db.AuditLogs.AsQueryable();

                if (!string.IsNullOrWhiteSpace(action))
                    query = query.Where(a => a.Action == action);

                var total = await query.CountAsync();
                var logs  = await query
                    .OrderByDescending(a => a.Created_at)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("AUDIT | ACTION=GET_AUDIT_LOGS | STATUS=SUCCESS | TOTAL={Total} | IP={Ip}", total, GetClientIp());
                return Ok(new { total, page, pageSize, logs });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AUDIT | ACTION=GET_AUDIT_LOGS | STATUS=ERROR | MESSAGE={Msg} | IP={Ip}", ex.Message, GetClientIp());
                return StatusCode(500, "An error occurred while fetching audit logs.");
            }
        }

        // POST api/feedback/log-action
        [HttpPost("log-action")]
        public async Task<IActionResult> LogAction([FromBody] UserActionLog log)
        {
            if (string.IsNullOrWhiteSpace(log.Action))
                return BadRequest("Action is required.");

            log.Action  = log.Action.Length  > 100 ? log.Action[..100]  : log.Action;
            log.Details = log.Details?.Length > 500 ? log.Details[..500] : log.Details;

            _logger.LogInformation("AUDIT | ACTION={Action} | STATUS=USER_INTERACTION | DETAILS={Details} | IP={Ip}",
                log.Action, log.Details ?? "none", GetClientIp());

            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Action     = log.Action,
                    Entity     = "UserInteraction",
                    EntityId   = log.EntityId,
                    Details    = log.Details,
                    IpAddress  = GetClientIp(),
                    Success    = true,
                    Created_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AUDIT | ACTION=LOG_ACTION | STATUS=WRITE_FAILED | MESSAGE={Msg}", ex.Message);
                return StatusCode(500, "Failed to save log.");
            }
        }
    }

    public class UserActionLog
    {
        public string Action   { get; set; } = string.Empty;
        public string? Details { get; set; }
        public int? EntityId   { get; set; }
    }
}