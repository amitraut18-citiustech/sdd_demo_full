using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriorAuth.API.Data;
using PriorAuth.API.DTOs;
using PriorAuth.API.Models;

namespace PriorAuth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EligibilityController : ControllerBase
{
    private readonly PriorAuthDbContext _db;
    private readonly ILogger<EligibilityController> _logger;

    public EligibilityController(PriorAuthDbContext db, ILogger<EligibilityController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("check")]
    public async Task<ActionResult<EligibilityCheckResponse>> Check([FromBody] EligibilityCheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId))
            return BadRequest("patientId is required.");

        if (request.HealthPlanId <= 0)
            return BadRequest("healthPlanId must be a positive integer.");

        Guid correlationGuid;
        if (request.CorrelationId != null)
        {
            if (!Guid.TryParse(request.CorrelationId, out correlationGuid))
                return BadRequest("correlationId must be a valid GUID.");
        }
        else
        {
            correlationGuid = Guid.NewGuid();
        }
        var correlationId = correlationGuid.ToString();
        var checkedAt = DateTime.UtcNow;

        string status;
        string? errorCode = null;
        string? errorMessage = null;

        // Member is checked first — if both the member and health plan are
        // missing, MBR-001 takes precedence (spec.md FR-04/OQ-09).
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.PatientId == request.PatientId);

        if (member == null)
        {
            status = "ERROR";
            errorCode = "MBR-001";
            errorMessage = "Member not found.";
        }
        else
        {
            var healthPlan = await _db.HealthPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.HealthPlanId == request.HealthPlanId);

            if (healthPlan == null)
            {
                status = "ERROR";
                errorCode = "PLN-001";
                errorMessage = "Health plan not found.";
            }
            else if (member.PlanCode == healthPlan.PlanCode)
            {
                status = "ELIGIBLE";
            }
            else
            {
                status = "INELIGIBLE";
            }
        }

        _db.EligibilityRecords.Add(new EligibilityRecord
        {
            CorrelationId = correlationGuid,
            Status = status,
            CheckedAt = checkedAt,
            DataSource = "LOCAL_DB"
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Eligibility check completed: {CorrelationId} {Status}", correlationId, status);

        return Ok(new EligibilityCheckResponse(status, correlationId, checkedAt, errorCode, errorMessage));
    }
}
