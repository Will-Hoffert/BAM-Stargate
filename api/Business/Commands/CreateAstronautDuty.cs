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
        public required string Name { get; set; }

        public required string Rank { get; set; }

        public required string DutyTitle { get; set; }

        public DateTime DutyStartDate { get; set; }
    }

    public class CreateAstronautDutyPreProcessor : IRequestPreProcessor<CreateAstronautDuty>
    {
        private readonly StargateContext _context;

        public CreateAstronautDutyPreProcessor(StargateContext context)
        {
            _context = context;
        }

        public Task Process(CreateAstronautDuty request, CancellationToken cancellationToken)
        {
            var person = _context.People.AsNoTracking().FirstOrDefault(z => z.Name == request.Name);

            if (person is null) throw new BadHttpRequestException($"No person named '{request.Name}' exists.", StatusCodes.Status404NotFound);

            // Start dates are stored without the time of day, so compare against the date only.
            var verifyNoPreviousDuty = _context.AstronautDuties.FirstOrDefault(z => z.PersonId == person.Id && z.DutyTitle == request.DutyTitle && z.DutyStartDate == request.DutyStartDate.Date);

            if (verifyNoPreviousDuty is not null) throw new BadHttpRequestException($"'{person.Name}' already has a {request.DutyTitle} duty starting {request.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.", StatusCodes.Status409Conflict);

            var currentDuty = _context.AstronautDuties.AsNoTracking()
                .Where(z => z.PersonId == person.Id)
                .OrderByDescending(z => z.DutyStartDate)
                .FirstOrDefault();

            // RETIRED is terminal, so a retired person's duty history is complete.
            if (currentDuty is not null && DutyRules.IsRetirement(currentDuty.DutyTitle)) throw new BadHttpRequestException($"'{person.Name}' retired on {currentDuty.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} and can't be given new duties.", StatusCodes.Status409Conflict);

            // The handler ends the current duty the day before the new one starts, so the new one has to start later.
            if (currentDuty is not null && request.DutyStartDate.Date <= currentDuty.DutyStartDate) throw new BadHttpRequestException($"A new duty for '{person.Name}' must start after their current duty, which started {currentDuty.DutyStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.", StatusCodes.Status409Conflict);

            // Rule 7 ends a career the day before RETIRED starts, which only works for someone who has served.
            if (currentDuty is null && DutyRules.IsRetirement(request.DutyTitle)) throw new BadHttpRequestException($"'{person.Name}' has no astronaut duties yet, so RETIRED can't be their first duty.", StatusCodes.Status400BadRequest);

            return Task.CompletedTask;
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

                await _context.AstronautDetails.AddAsync(astronautDetail);

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

            await _context.AstronautDuties.AddAsync(newAstronautDuty);

            await _context.SaveChangesAsync();

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
