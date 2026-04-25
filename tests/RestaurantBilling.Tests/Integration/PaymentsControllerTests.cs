using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Controllers;
using Data.Persistence;
using Entities.Enums;
using Entities.Integration;
using Entities.Sales;
using System.Security.Cryptography;
using System.Text;

namespace RestaurantBilling.IntegrationTests;

public class PaymentsControllerTests
{
    [Fact]
    public async Task PhonePeCallback_ReturnsOk_WhenSignatureMatches()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var raw = payload + "/callback" + "salt";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var verify = $"{hash}###1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, verify, "evt_1", timestamp, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_1");
        Assert.True(evt.IsValid);
        Assert.Equal("Verified", evt.ProcessingStatus);
        Assert.NotNull(evt.ProcessedAtUtc);
    }

    [Fact]
    public async Task PhonePeCallback_ReturnsUnauthorized_WhenTimestampStale()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "1"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var oldTs = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, "bad", "evt_2", oldTs, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task PhonePeCallback_PersistsRejectedState_WhenSignatureInvalid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, "invalid", "evt_3", ts, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_3");
        Assert.False(evt.IsValid);
        Assert.Equal("Rejected", evt.ProcessingStatus);
        Assert.NotNull(evt.ProcessedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(evt.ErrorMessage));
    }

    [Fact]
    public async Task PhonePeCallback_MarksApplied_WhenMatchingPaymentReferenceExists()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var bill = new Bill(1, "B-1001", DateOnly.FromDateTime(DateTime.UtcNow), BillType.DineIn);
        bill.Settle([new Payment(PaymentMode.UPI, 100m, referenceNo: "evt_apply_1")]);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();

        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var raw = payload + "/callback" + "salt";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var verify = $"{hash}###1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, verify, "evt_apply_1", timestamp, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_apply_1");
        Assert.True(evt.IsValid);
        Assert.Equal("Applied", evt.ProcessingStatus);
        Assert.NotNull(evt.MatchedPaymentId);
        Assert.NotNull(evt.MatchedBillId);
    }

    [Fact]
    public async Task PhonePeCallback_MarksSettled_WhenCallbackAmountMatchesPayment()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var bill = new Bill(1, "B-1002", DateOnly.FromDateTime(DateTime.UtcNow), BillType.DineIn);
        bill.Settle([new Payment(PaymentMode.UPI, 250m, referenceNo: "evt_settled_1")]);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();

        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var raw = payload + "/callback" + "salt";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var verify = $"{hash}###1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, verify, "evt_settled_1", timestamp, CancellationToken.None, "250.00");

        Assert.IsType<OkObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_settled_1");
        Assert.Equal("Settled", evt.ProcessingStatus);
        Assert.Null(evt.ErrorMessage);
    }

    [Fact]
    public async Task PhonePeCallback_MarksAmountMismatch_WhenCallbackAmountDiffers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var bill = new Bill(1, "B-1003", DateOnly.FromDateTime(DateTime.UtcNow), BillType.DineIn);
        bill.Settle([new Payment(PaymentMode.UPI, 300m, referenceNo: "evt_mismatch_1")]);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();

        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "abc";
        var raw = payload + "/callback" + "salt";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var verify = $"{hash}###1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, verify, "evt_mismatch_1", timestamp, CancellationToken.None, "299.00");

        Assert.IsType<OkObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_mismatch_1");
        Assert.Equal("AmountMismatch", evt.ProcessingStatus);
        Assert.False(string.IsNullOrWhiteSpace(evt.ErrorMessage));
    }

    [Fact]
    public async Task PhonePeCallback_ParsesAmountFromPayload_WhenHeaderMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentCallbacks:PhonePeSaltKey"] = "salt",
                ["PaymentCallbacks:PhonePeSaltIndex"] = "1",
                ["PaymentCallbacks:TimestampToleranceMinutes"] = "5"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var bill = new Bill(1, "B-1004", DateOnly.FromDateTime(DateTime.UtcNow), BillType.DineIn);
        bill.Settle([new Payment(PaymentMode.UPI, 120m, referenceNo: "evt_payload_amount_1")]);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();

        var controller = new PaymentsController(config, db, NullLogger<PaymentsController>.Instance);
        var payload = "{\"amount\":120.00}";
        var raw = payload + "/callback" + "salt";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var verify = $"{hash}###1";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await controller.PhonePeCallback(payload, verify, "evt_payload_amount_1", timestamp, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var evt = await db.PaymentCallbackEvents.SingleAsync(x => x.EventId == "evt_payload_amount_1");
        Assert.Equal("Settled", evt.ProcessingStatus);
        Assert.Null(evt.ErrorMessage);
    }
}
