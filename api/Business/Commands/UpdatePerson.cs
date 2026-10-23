using MediatR;
using MediatR.Pipeline;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Business.Rules;
using StargateAPI.Controllers;

namespace StargateAPI.Business.Commands
{
    // A person's only own data is their name: rank, title and career dates come from their duties.
    public class UpdatePerson : IRequest<UpdatePersonResult>
    {
        public required string Name { get; set; } = string.Empty;

        public required string NewName { get; set; } = string.Empty;
    }

    public class UpdatePersonPreProcessor : IRequestPreProcessor<UpdatePerson>
    {
        private readonly StargateContext _context;
        public UpdatePersonPreProcessor(StargateContext context)
        {
            _context = context;
        }
        public async Task Process(UpdatePerson request, CancellationToken cancellationToken)
        {
            // The request's own values are checked before any database read, and written back so the checks below and the handler use them.
            // The current name only identifies an existing person, so it isn't held to the length limit for new names.
            request.Name = PersonRules.RequireName(request.Name);
            request.NewName = PersonRules.ValidateName(request.NewName);

            var person = await _context.People.AsNoTracking().FirstOrDefaultAsync(z => z.Name == request.Name, cancellationToken);

            if (person is null) throw new BadHttpRequestException($"No person named '{request.Name}' exists.", StatusCodes.Status404NotFound);

            // Names compare without case, so the person's own row is left out: otherwise fixing the capitalization of a name would be refused.
            var nameTaken = await _context.People.AnyAsync(z => z.Name == request.NewName && z.Id != person.Id, cancellationToken);

            if (nameTaken) throw new BadHttpRequestException($"A person named '{request.NewName}' already exists.", StatusCodes.Status409Conflict);
        }
    }

    public class UpdatePersonHandler : IRequestHandler<UpdatePerson, UpdatePersonResult>
    {
        private readonly StargateContext _context;

        public UpdatePersonHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<UpdatePersonResult> Handle(UpdatePerson request, CancellationToken cancellationToken)
        {
            var person = await _context.People.FirstOrDefaultAsync(z => z.Name == request.Name, cancellationToken);

            // UpdatePersonPreProcessor checked this already, but another rename can land before the handler runs.
            if (person is null) throw new BadHttpRequestException($"No person named '{request.Name}' exists.", StatusCodes.Status404NotFound);

            // Renaming someone to exactly the name they have leaves EF nothing to save.
            person.Name = request.NewName;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // Another request saved this name after UpdatePersonPreProcessor checked it.
                throw new BadHttpRequestException($"A person named '{request.NewName}' already exists.", StatusCodes.Status409Conflict, ex);
            }

            return new UpdatePersonResult()
            {
                Id = person.Id
            };
        }
    }

    public class UpdatePersonResult : BaseResponse
    {
        public int Id { get; set; }
    }
}
