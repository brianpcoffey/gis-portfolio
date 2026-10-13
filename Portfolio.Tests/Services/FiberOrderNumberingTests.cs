using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Repositories;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    // FiberOrderService + the real FiberOrderRepository over the in-memory provider: the order numbers
    // CreateAsync issues across several creates, users and days.
    public class FiberOrderNumberingTests
    {
        private readonly PortfolioDbContext _db = new(new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase($"FiberOrderNumbering_{Guid.NewGuid()}")
            .Options);
        private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

        private FiberOrderService ServiceFor(Guid userId)
        {
            var users = new Mock<IUserProfileService>();
            users.Setup(u => u.GetCurrentUserId()).Returns(userId);
            return new FiberOrderService(new FiberOrderRepository(_db), users.Object, _clock);
        }

        private static CreateFiberOrderDto Dto() => new()
        {
            ClientName = "Acme",
            ProductName = "Cable",
            Quantity = 1,
            UnitPrice = 1m,
            Status = "Pending"
        };

        [Fact]
        public async Task ConsecutiveCreates_CountUpFromOne()
        {
            // Arrange
            var service = ServiceFor(Guid.NewGuid());

            // Act
            var first = await service.CreateAsync(Dto());
            var second = await service.CreateAsync(Dto());
            var third = await service.CreateAsync(Dto());

            // Assert
            Assert.Equal(["ORD-20261013-0001", "ORD-20261013-0002", "ORD-20261013-0003"],
                [first.OrderNumber, second.OrderNumber, third.OrderNumber]);
        }

        [Fact]
        public async Task EachUserHasTheirOwnSequence()
        {
            // Arrange
            var a = ServiceFor(Guid.NewGuid());
            var b = ServiceFor(Guid.NewGuid());
            await a.CreateAsync(Dto());
            await a.CreateAsync(Dto());

            // Act
            var firstForB = await b.CreateAsync(Dto());
            var thirdForA = await a.CreateAsync(Dto());

            // Assert
            Assert.Equal("ORD-20261013-0001", firstForB.OrderNumber);
            Assert.Equal("ORD-20261013-0003", thirdForA.OrderNumber);
        }

        [Fact]
        public async Task TheSequenceRestartsEachUtcDay()
        {
            // Arrange
            var service = ServiceFor(Guid.NewGuid());
            await service.CreateAsync(Dto());
            await service.CreateAsync(Dto());
            _clock.Advance(TimeSpan.FromDays(1));

            // Act
            var nextDay = await service.CreateAsync(Dto());

            // Assert
            Assert.Equal("ORD-20261014-0001", nextDay.OrderNumber);
        }

        [Fact]
        public async Task DeletingAnEarlierOrder_DoesNotMakeTheNextNumberCollideWithALaterOne()
        {
            // Arrange
            var service = ServiceFor(Guid.NewGuid());
            var first = await service.CreateAsync(Dto());
            await service.CreateAsync(Dto());
            await service.DeleteAsync(first.Id);

            // Act
            var next = await service.CreateAsync(Dto());

            // Assert
            var all = await service.GetAllAsync();
            Assert.Equal("ORD-20261013-0003", next.OrderNumber);
            Assert.Equal(all.Count, all.Select(o => o.OrderNumber).Distinct().Count());
        }

        [Fact]
        public async Task OrdersWithNoNumber_FromBeforeNumbersWereIssued_AreIgnored()
        {
            // Arrange: a row as production holds today, with an empty number.
            var userId = Guid.NewGuid();
            _db.FiberOrders.Add(new FiberOrder { UserId = userId, ClientName = "Legacy", ProductName = "Cable" });
            await _db.SaveChangesAsync();

            // Act
            var created = await ServiceFor(userId).CreateAsync(Dto());

            // Assert
            Assert.Equal("ORD-20261013-0001", created.OrderNumber);
        }
    }
}
