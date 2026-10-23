using Dapper;
using MediatR;
using StargateAPI.Business.Data;
using StargateAPI.Business.Dtos;
using StargateAPI.Controllers;
using StargateAPI.Infrastructure;

namespace StargateAPI.Business.Queries
{
    public class GetProcessLog : IRequest<GetProcessLogResult>, ISkipProcessLogging
    {
        public const int DefaultTake = 50;

        public const int MaxTake = 200;

        // How many of the newest entries to return; null means DefaultTake.
        public int? Take { get; set; }
    }

    public class GetProcessLogHandler : IRequestHandler<GetProcessLog, GetProcessLogResult>
    {
        private readonly StargateContext _context;
        public GetProcessLogHandler(StargateContext context)
        {
            _context = context;
        }
        public async Task<GetProcessLogResult> Handle(GetProcessLog request, CancellationToken cancellationToken)
        {
            var result = new GetProcessLogResult();

            // Ids only grow (AUTOINCREMENT), so the highest Ids are the newest entries. Exception is not selected.
            var query = "SELECT Id, TimestampUtc, Level, Operation, Message, RequestJson, DurationMs FROM [ProcessLog] ORDER BY Id DESC LIMIT @Take";

            // A size limit, not a rule about the data, so a value out of range is brought into range rather than rejected.
            var take = Math.Clamp(request.Take ?? GetProcessLog.DefaultTake, 1, GetProcessLog.MaxTake);

            var entries = await _context.Connection.QueryAsync<ProcessLogEntryDto>(new CommandDefinition(query, new { Take = take }, cancellationToken: cancellationToken));

            result.Entries = entries.ToList();

            foreach (var entry in result.Entries)
            {
                // SQLite hands the stored time back without a time zone. Marking it UTC makes the JSON end in Z,
                // so a browser doesn't read it as the viewer's local time.
                entry.TimestampUtc = DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc);
            }

            return result;
        }
    }

    public class GetProcessLogResult : BaseResponse
    {
        public List<ProcessLogEntryDto> Entries { get; set; } = new List<ProcessLogEntryDto> { };
    }
}
