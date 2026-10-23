using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Rules;
using StargateAPI.Controllers;
using System.Globalization;
using System.Net;

namespace StargateAPI.Business.Commands
{
    public class CreateAstronautDuty : IRequest<CreateAstronautDutyResult>
    {
        // Not 'required': a missing field reaches CreateAstronautDutyPreProcessor and gets the same message as a blank one.
        public string Name { get; set; } = string.Empty;

        public string Rank { get; set; } = string.Empty;

        public string DutyTitle { get; set; } = string.Empty;

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public async Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            // The request's own values are checked before any database read, and written back so every later check and the handler use them.
            // The name only identifies an existing person, so it isn't held to the length limit for new names.
            request.Name = PersonRules.RequireName(request.Name);
            request.Rank = DutyRules.ValidateRank(request.Rank);
            request.DutyTitle = DutyRules.ValidateDutyTitle(request.DutyTitle);
            request.DutyStartDate = DutyRules.ValidateStartDate(request.DutyStartDate);

            var person = await _context.People.AsNoTracking().FirstOrDefaultAsync(z => z.Name == request.Name, cancellationToken);

            if (person is null) throw new BadHttpRequestException($"No person named '{request.Name}' exists.", StatusCodes.Status404NotFound);

            // Start dates are stored without the time of day, so compare against the date only.
            var verifyNoPreviousDuty = await _context.AstronautDuties.FirstOrDefaultAsync(z => z.PersonId == person.Id && z.DutyTitle == request.DutyTitle && z.DutyStartDate == request.DutyStartDate.Date, cancellationToken);

            if (verifyNoPreviousDuty is not null) throw new BadHttpRequestException($"'{person.Name}' already has a {request.DutyTitle} duty starting {request.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.", StatusCodes.Status409Conflict);

            var currentDuty = await _context.AstronautDuties.AsNoTracking()
                .Where(z => z.PersonId == person.Id)
                .OrderByDescending(z => z.DutyStartDate)
                .FirstOrDefaultAsync(cancellationToken);

            // RETIRED is terminal, so a retired person's duty history is complete.
            if (currentDuty is not null && DutyRules.IsRetirement(currentDuty.DutyTitle)) throw new BadHttpRequestException($"'{person.Name}' retired on {currentDuty.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} and can't be given new duties.", StatusCodes.Status409Conflict);

            // The handler ends the current duty the day before the new one starts, so the new one has to start later.
            if (currentDuty is not null && request.DutyStartDate.Date <= currentDuty.DutyStartDate) throw new BadHttpRequestException($"A new duty for '{person.Name}' must start after their current duty, which started {currentDuty.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.", StatusCodes.Status409Conflict);

            // Rule 7 ends a career the day before RETIRED starts, which only works for someone who has served.
            if (currentDuty is null && DutyRules.IsRetirement(request.DutyTitle)) throw new BadHttpRequestException($"'{person.Name}' has no astronaut duties yet, so RETIRED can't be their first duty.", StatusCodes.Status400BadRequest);
        }
    }

    public class CreateAstronautDutyHandler : IRequestHandler<CreateAstronautDuty, CreateAstronautDutyResult>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<CreateAstronautDutyResult> Handle(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var person = await _context.People
                .Include(z => z.AstronautDetail)
                .FirstOrDefaultAsync(z => z.Name == request.Name, cancellationToken);

            // CreateAstronautDutyPreProcessor checked this already, but the person can change before the handler runs.
            if (person is null) throw new BadHttpRequestException($"No person named '{request.Name}' exists.", StatusCodes.Status404NotFound);

            var isRetirement = DutyRules.IsRetirement(request.DutyTitle);
            // Store retirements with one spelling so later checks and readers can match it exactly.
            var dutyTitle = isRetirement ? DutyRules.Retired : request.DutyTitle;

            var astronautDetail = person.AstronautDetail;

            if (astronautDetail == null)
            {
                astronautDetail = new AstronautDetail();
                astronautDetail.PersonId = person.Id;
                astronautDetail.CurrentDutyTitle = dutyTitle;
                astronautDetail.CurrentRank = request.Rank;
                astronautDetail.CareerStartDate = request.DutyStartDate.Date;
                // CreateAstronautDutyPreProcessor rejects RETIRED as a first duty, so a first duty never ends a career.

                await _context.AstronautDetails.AddAsync(astronautDetail, cancellationToken);

            }
            else
            {
                astronautDetail.CurrentDutyTitle = dutyTitle;
                astronautDetail.CurrentRank = request.Rank;
                if (isRetirement)
                {
                    astronautDetail.CareerEndDate = request.DutyStartDate.AddDays(-1).Date;
                }
            }

            var astronautDuty = await _context.AstronautDuties
                .Where(z => z.PersonId == person.Id)
                .OrderByDescending(z => z.DutyStartDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (astronautDuty != null)
            {
                astronautDuty.DutyEndDate = request.DutyStartDate.AddDays(-1).Date;
            }

            var newAstronautDuty = new AstronautDuty()
            {
                PersonId = person.Id,
                Rank = request.Rank,
                DutyTitle = dutyTitle,
                DutyStartDate = request.DutyStartDate.Date,
                DutyEndDate = null
            };

            // Close the previous duty in its own save before inserting the new one, so the current-duty index never sees
            // two open duties; the transaction undoes that save if the insert fails. It starts after the reads on purpose:
            // a request that lost a race for this person fails on the index instead of waiting and closing the winner's duty.
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                await _context.AstronautDuties.AddAsync(newAstronautDuty, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // Another request saved a duty for this person after this one read the current duty.
                throw new BadHttpRequestException($"Another duty for '{person.Name}' was saved at the same time. Check their current duty and try again.", StatusCodes.Status409Conflict, ex);
            }

            return new CreateAstronautDutyResult()
            {
                Id = newAstronautDuty.Id
            };
        }
    }

    public class CreateAstronautDutyResult : BaseResponse
    {
        public int? Id { get; set; }
    }
}
