using Halcyon.App.Features.Tickets;
using Halcyon.App.Messaging;
using Halcyon.App.Notifications;
using Halcyon.Database;
using Halcyon.Database.Documents;
using Halcyon.Domain;
using Halcyon.Domain.Tickets;
using Halcyon.Domain.ValueObjects;
using Halcyon.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace Halcyon.IntegrationTests;

/// <summary>
/// Spec 013 on PostgreSQL 18 (Testcontainers): the ticket commands go through Wolverine's handlers, their mails through
/// the transactional outbox and the durable mail queue (ADR-0016). The mails are recorded instead of sent; Mailpit
/// covers real SMTP (<see cref="MailpitTests"/>).
/// </summary>
public class NotificationTests(PostgresContainer postgres) : TicketTestBase(postgres)
{
    private const string Pool = "zespol@company.io";

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        // A second host with the pool's mailbox and links, for the deadline mails.
        poolApp = await TestApp.StartAsync(
            Postgres,
            settings: new() { ["Mail:PoolAddress"] = Pool, ["Mail:BaseUrl"] = "https://halcyon.test" },
            now: Now
        );
        await poolApp.SeedPeopleAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await poolApp.DisposeAsync();
        await base.DisposeAsync();
    }

    private TestApp poolApp = null!;

    private static async Task<Guid> ReportOnAsync(TestApp app, ReportTicketCommand command) =>
        (await app.Get<ReportTicket>().ByAgentAsync(await app.AgentAsync("anna.nowak@company.io"), command)).TicketId;

    // ------------------------------------------------------------------------------------------------ requester

    [Fact]
    public async Task AReport_ForAPerson_SendsTheConfirmation_WithTheNumberAndTheDeadline()
    {
        var id = await ReportAsync(Priority.High, title: "Drukarka nie drukuje");

        var mail = (await App.Mail.WaitForAsync(Adam.Email)).Single();
        var view = await ViewAsync(id);
        mail.Subject.ShouldBe($"Przyjęliśmy zgłoszenie {view.Number}: Drukarka nie drukuje");
        mail.ToName.ShouldBe("Adam Mazur");
        mail.Text.ShouldContain("Termin realizacji: 03.11.2026 19:00"); // 10:00 UTC + 8 h, in Warsaw
        mail.Text.ShouldContain("Priorytet: Wysoki");
        mail.Text.ShouldNotContain("/zgloszenia/"); // requesters do not sign in
        mail.Html.ShouldContain("Drukarka nie drukuje");
    }

    [Fact]
    public void AnIncognitoReport_PlansNoConfirmation()
    {
        var reported = Ticket.Report(
            Guid.CreateVersion7(),
            new TicketDraft(
                "A",
                "B",
                "it",
                "it.hardware",
                Priority.Low,
                Now,
                Requester.Anonymous,
                ReportChannel.SelfService,
                null
            ),
            Now
        );

        TicketMailPlan.Reported(reported, Now).OfType<ReportConfirmationMail>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolving_TellsTheRequester()
    {
        var id = await ReportAsync();
        await App.Get<TakeTicket>().ExecuteAsync(id, Anna);
        await App.Get<ResolveTicket>().ExecuteAsync(id, Anna);

        var mails = await App.Mail.WaitForAsync(Adam.Email, count: 2);
        mails.ShouldContain(m => m.Subject.StartsWith("Zrealizowano zgłoszenie", StringComparison.Ordinal));
        mails
            .Single(m => m.Subject.StartsWith("Zrealizowano", StringComparison.Ordinal))
            .Text.ShouldContain("przez: Anna Nowak");
    }

    // ------------------------------------------------------------------------------------------------ agents

    [Fact]
    public async Task AHandover_TellsTheNewAgent_WithWhoWhyAndALink()
    {
        var id = await ReportOnAsync(poolApp, Command());
        var anna = await poolApp.AgentAsync("anna.nowak@company.io");
        var piotr = await poolApp.AgentAsync("piotr.kowalski@company.io");
        await poolApp.Get<TakeTicket>().ExecuteAsync(id, anna);
        await poolApp.Get<HandOverTicket>().ExecuteAsync(id, anna, piotr.Id, "Urlop od jutra");

        var mail = (await poolApp.Mail.WaitForAsync("piotr.kowalski@company.io")).Single();
        mail.Subject.ShouldStartWith("Przekazano Ci zgłoszenie");
        mail.Text.ShouldContain("Anna Nowak przekazał(a) Ci zgłoszenie");
        mail.Text.ShouldContain("Powód: Urlop od jutra");
        mail.Text.ShouldContain($"https://halcyon.test/zgloszenia/{id}");
    }

    // ------------------------------------------------------------------------------------------------ deadlines

    [Fact]
    public async Task ATicketReportedPastItsDeadline_AlertsThePoolAtOnce()
    {
        // High = 8 h; reported 9 h ago and nobody has it: the pool's mailbox hears it now.
        var id = await ReportOnAsync(poolApp, Command(Priority.High, Now.AddHours(-9)));

        var mail = (await poolApp.Mail.WaitForAsync(Pool)).Single();
        mail.Subject.ShouldBe(
            $"Po terminie: {(await poolApp.Get<TicketQueries>().DetailsAsync(id))!.Ticket.Number} Laptop nie ładuje"
        );
        mail.Text.ShouldContain("Zgłoszenie czeka w puli");
    }

    [Fact]
    public async Task ATicketWithTimeLeft_HasItsWarningAndAlertScheduled_InTheDatabase()
    {
        var id = await ReportAsync();

        var scheduled = await ScheduledDeadlineMailsAsync(App, id);
        scheduled.Count.ShouldBe(2);
        // 3 days minus an hour, and 3 days, from now (the host's clock).
        scheduled.Min().ShouldBe(DateTimeOffset.UtcNow.AddDays(3).AddHours(-1), TimeSpan.FromMinutes(1));
        scheduled.Max().ShouldBe(DateTimeOffset.UtcNow.AddDays(3), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task APriorityChange_SchedulesNewMails_AndTheOldOnesStayQuiet()
    {
        var id = await ReportOnAsync(poolApp, Command(Priority.Low, Now.AddHours(-2)));
        var anna = await poolApp.AgentAsync("anna.nowak@company.io");
        await poolApp.Get<TakeTicket>().ExecuteAsync(id, anna);
        var oldDeadline = (await ViewOnAsync(poolApp, id)).Deadline;

        // Critical = 3 h from the report 2 h ago: an hour left - the warning goes to Anna now.
        await poolApp.Get<ChangeTicketPriority>().ExecuteAsync(id, anna, Priority.Critical, null);
        var warning = (await poolApp.Mail.WaitForAsync("anna.nowak@company.io")).Single();
        warning.Subject.ShouldStartWith("Została godzina:");

        // The alert scheduled for the old deadline, when its time comes: the deadline is no longer the ticket's.
        await poolApp
            .Services.GetRequiredService<IMessageBus>()
            .InvokeAsync(new DeadlineMail(Guid.CreateVersion7(), id, oldDeadline, DeadlineMailKind.Overdue));
        await Task.Delay(500);
        poolApp.Mail.Sent.Count(m => m.To == "anna.nowak@company.io").ShouldBe(1);
    }

    [Fact]
    public async Task ADeadlineMail_ForAResolvedTicket_StaysQuiet()
    {
        var id = await ReportOnAsync(poolApp, Command(Priority.High));
        var anna = await poolApp.AgentAsync("anna.nowak@company.io");
        await poolApp.Get<TakeTicket>().ExecuteAsync(id, anna);
        await poolApp.Get<ResolveTicket>().ExecuteAsync(id, anna);
        var deadline = (await ViewOnAsync(poolApp, id)).Deadline;

        await poolApp
            .Services.GetRequiredService<IMessageBus>()
            .InvokeAsync(new DeadlineMail(Guid.CreateVersion7(), id, deadline, DeadlineMailKind.Overdue));

        poolApp.Mail.Sent.ShouldNotContain(m => m.Subject.StartsWith("Po terminie", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------ delivery

    [Fact]
    public async Task TheSameMail_DeliveredTwice_LeavesOnce()
    {
        var id = await ReportAsync();
        await App.Mail.WaitForAsync(Adam.Email);
        var bus = App.Services.GetRequiredService<IMessageBus>();
        var resolved = new ResolvedMail(Guid.CreateVersion7(), id);

        await bus.InvokeAsync(resolved);
        await bus.InvokeAsync(resolved);

        App.Mail.Sent.Count(m => m.Subject.StartsWith("Zrealizowano", StringComparison.Ordinal)).ShouldBe(1);
        await using var session = App.Store.QuerySession();
        (await session.LoadAsync<SentMail>(resolved.MailId))!.Kind.ShouldBe(nameof(ResolvedMail));
    }

    [Fact]
    public async Task AFlakySmtpServer_IsTriedAgain_AndTheMailLeavesOnce()
    {
        App.Mail.FailNext(2);

        await ReportAsync();

        // Retried after 1 s and 5 s (MailFailurePolicies): the third attempt succeeds.
        var mails = await App.Mail.WaitForAsync(Adam.Email, timeout: TimeSpan.FromSeconds(20));
        mails.Count.ShouldBe(1);
        App.Mail.Attempts.ShouldBe(3);
    }

    [Fact]
    public async Task ACommandThatFails_SendsNothing()
    {
        // The domain refuses (no title) after the requester was found: no event, no mail - one transaction.
        await Should.ThrowAsync<DomainException>(() =>
            App.Get<ReportTicket>().ByAgentAsync(Anna, Command(title: "  "))
        );
        // A report that does go through, and its mail: the refused one never queued one before it.
        await ReportAsync();
        await App.Mail.WaitForAsync(Adam.Email);
        await Task.Delay(500);
        App.Mail.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TheMailsWait_InTheDatabase_NotInMemory()
    {
        await ReportAsync(Priority.Low);

        await using var connection = await App.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        await using var tables = new NpgsqlCommand(
            $"select count(*) from information_schema.tables where table_schema = '{HalcyonStore.MessageSchema}'",
            connection
        );
        ((long)(await tables.ExecuteScalarAsync())!).ShouldBeGreaterThan(0);
        (await ScheduledDeadlineMailsAsync(App, null)).Count.ShouldBeGreaterThan(0);
    }

    // ------------------------------------------------------------------------------------------------ helpers

    private static async Task<TicketView> ViewOnAsync(TestApp app, Guid id)
    {
        await using var session = app.Store.QuerySession();
        return (await session.LoadAsync<TicketView>(id))!;
    }

    /// <summary>When the deadline mails of a ticket (or of any) are due, from Wolverine's durable inbox.</summary>
    private static async Task<List<DateTimeOffset>> ScheduledDeadlineMailsAsync(TestApp app, Guid? ticketId)
    {
        await using var connection = await app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            $"""
            select execution_time, body, received_at from {HalcyonStore.MessageSchema}.wolverine_incoming_envelopes
            where status = 'Scheduled' and message_type = '{typeof(DeadlineMail).FullName}'
            """,
            connection
        );
        var times = new List<DateTimeOffset>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var body = System.Text.Encoding.UTF8.GetString((byte[])reader["body"]);
            // In the one durable mail queue, not in a queue of its own type.
            reader.GetString(2).ShouldBe($"local://{WolverineSetup.MailQueue}/");
            if (ticketId is null || body.Contains(ticketId.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                times.Add(reader.GetFieldValue<DateTimeOffset>(0));
        }
        return times;
    }
}

/// <summary>Spec 013 over real SMTP: Mailpit in a container.</summary>
public class MailpitTests(PostgresContainer postgres, MailpitContainer mailpit) : IClassFixture<MailpitContainer>
{
    [Fact]
    public async Task TheConfirmation_ArrivesAtTheSmtpServer_FromTheConfiguredSender()
    {
        await using var app = await TestApp.StartAsync(
            postgres,
            settings: new(mailpit.Settings)
            {
                ["Mail:FromAddress"] = "helpdesk@company.io",
                ["Mail:FromName"] = "Helpdesk",
            }
        );
        await app.SeedPeopleAsync();
        var anna = await app.AgentAsync("anna.nowak@company.io");
        var person = await app.PersonAsync("barbara.krawczyk@company.io");

        var reported = await app.Get<ReportTicket>()
            .ByAgentAsync(
                anna,
                new ReportTicketCommand(
                    "Brak dostępu do VPN",
                    "Od rana.",
                    "it",
                    "it.application",
                    Priority.Medium,
                    DateTimeOffset.UtcNow,
                    person.Id,
                    false
                )
            );

        var message = (await mailpit.WaitForAsync(person.Email)).Single();
        message.Subject.ShouldBe(
            $"Przyjęliśmy zgłoszenie {TicketNumber.For(reported.TicketId).Value}: Brak dostępu do VPN"
        );
        message.From.Address.ShouldBe("helpdesk@company.io");
        message.From.Name.ShouldBe("Helpdesk");
        (await mailpit.TextOfAsync(message)).ShouldContain("Termin realizacji:");
    }

    [Theory]
    [InlineData("Mail:Smtp:Host", "", "Mail:Smtp:Host is required")]
    [InlineData("Mail:BaseUrl", "halcyon.local", "Mail:BaseUrl must be an absolute http(s) address")]
    [InlineData("Mail:FromAddress", "helpdesk", "Mail:FromAddress must be an e-mail address")]
    [InlineData("Mail:Smtp:Username", "user", "Mail:Smtp:Username and Mail:Smtp:Password go together")]
    public async Task AWrongMailSetting_StopsTheHost_NamingIt(string key, string value, string reason)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        (await Migrator.RunAsync(connectionString)).ShouldBe(0);
        await using var host = TestApp.Build(connectionString, new(mailpit.Settings) { [key] = value }, null, out _);

        var refusal = await Should.ThrowAsync<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            host.StartAsync()
        );
        refusal.Message.ShouldContain(reason);
    }
}
