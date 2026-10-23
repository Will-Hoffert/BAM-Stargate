using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Business.Queries;

namespace StargateAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProcessLogController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProcessLogController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetProcessLog([FromQuery] int? take, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetProcessLog()
            {
                Take = take
            }, cancellationToken);

            return this.GetResponse(result);
        }
    }
}
