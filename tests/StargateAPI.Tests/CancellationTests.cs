using System.Net.Http.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StargateAPI.Business.Commands;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;

namespace StargateAPI.Tests
{
    // A request whose caller has gone away stops at its next database call. Over HTTP these requests finish in
    // milliseconds, so the tests call the controllers, pre-processors and handlers directly with a token that is
    // already cancelled. Each test uses its own person.
    public class CancellationTests : IClassFixture<StargateApiFactory>
    {
        private static readonly CancellationToken CancelledToken = new(canceled: true);

        private readonly StargateApiFactory _factory;
        private readonly HttpClient _client;

        public CancellationTests(StargateApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetPeople_CancelledRequest_Throws()
        {
            using var scope = _factory.Services.CreateScope();
            var controller = new PersonController(scope.ServiceProvider.GetRequiredService<IMediator>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.GetPeople(CancelledToken));
        }

        [Fact]
        public async Task GetPersonByName_CancelledRequest_Throws()
        {
            await CreatePersonAsync("Cancelled Lookup Person");
            using var scope = _factory.Services.CreateScope();
            var controller = new PersonController(scope.ServiceProvider.GetRequiredService<IMediator>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.GetPersonByName("Cancelled Lookup Person", CancelledToken));
        }

        [Fact]
        public async Task CreatePerson_CancelledRequest_ThrowsAndSavesNothing()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var controller = new PersonController(scope.ServiceProvider.GetRequiredService<IMediator>());

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.CreatePerson("Cancelled New Person", CancelledToken));
            }

            Assert.False(await PersonExistsAsync("Cancelled New Person"));
        }

        [Fact]
        public async Task GetAstronautDutiesByName_CancelledRequest_Throws()
        {
            await CreatePersonAsync("Cancelled Duties Person");
            using var scope = _factory.Services.CreateScope();
            var controller = new AstronautDutyController(scope.ServiceProvider.GetRequiredService<IMediator>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.GetAstronautDutiesByName("Cancelled Duties Person", CancelledToken));
        }

        [Fact]
        public async Task CreateAstronautDuty_CancelledRequest_ThrowsAndSavesNothing()
        {
            await CreatePersonAsync("Cancelled Duty Person");
            using (var scope = _factory.Services.CreateScope())
            {
                var controller = new AstronautDutyController(scope.ServiceProvider.GetRequiredService<IMediator>());

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.CreateAstronautDuty(DutyRequest("Cancelled Duty Person"), CancelledToken));
            }

            using var assertScope = _factory.Services.CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<StargateContext>();
            Assert.False(await context.AstronautDuties.AnyAsync(d => d.Person.Name == "Cancelled Duty Person"));
        }

        [Fact]
        public async Task CreatePersonPreProcessor_CancelledToken_Throws()
        {
            using var scope = _factory.Services.CreateScope();
            var preProcessor = new CreatePersonPreProcessor(scope.ServiceProvider.GetRequiredService<StargateContext>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preProcessor.Process(new CreatePerson { Name = "Cancelled Check Person" }, CancelledToken));
        }

        [Fact]
        public async Task CreateAstronautDutyPreProcessor_CancelledToken_Throws()
        {
            await CreatePersonAsync("Cancelled Duty Check Person");
            using var scope = _factory.Services.CreateScope();
            var preProcessor = new CreateAstronautDutyPreProcessor(scope.ServiceProvider.GetRequiredService<StargateContext>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preProcessor.Process(DutyRequest("Cancelled Duty Check Person"), CancelledToken));
        }

        [Fact]
        public async Task CreatePersonHandler_CancelledToken_SavesNothing()
        {
            // CreatePersonPreProcessor would stop a cancelled request first, so the handler is called directly.
            using (var scope = _factory.Services.CreateScope())
            {
                var handler = new CreatePersonHandler(scope.ServiceProvider.GetRequiredService<StargateContext>());

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new CreatePerson { Name = "Cancelled Save Person" }, CancelledToken));
            }

            Assert.False(await PersonExistsAsync("Cancelled Save Person"));
        }

        private async Task CreatePersonAsync(string name)
        {
            var response = await _client.PostAsJsonAsync("/Person", name);
            response.EnsureSuccessStatusCode();
        }

        private async Task<bool> PersonExistsAsync(string name)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StargateContext>();
            return await context.People.AnyAsync(p => p.Name == name);
        }

        private static CreateAstronautDuty DutyRequest(string name)
        {
            return new CreateAstronautDuty
            {
                Name = name,
                Rank = "CPT",
                DutyTitle = "Pilot",
                DutyStartDate = new DateTime(2020, 1, 1)
            };
        }
    }
}
