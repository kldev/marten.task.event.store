using Halcyon.Domain.Categories;
using Halcyon.Domain.People;

namespace Halcyon.Domain.Tickets;

/// <summary>What a new ticket is made of, before the rules have looked at it.</summary>
public sealed record TicketDraft(
    string? Title,
    string? Description,
    string? CategoryKey,
    string? SubcategoryKey,
    Priority? Priority,
    DateTimeOffset ReportedAt,
    Requester Requester,
    ReportChannel Channel,
    AgentRef? RegisteredBy,
    IReadOnlyDictionary<string, string?>? Fields = null
);

/// <summary>
/// A request somebody made of the IT, legal or marketing team, from the moment it was reported until it is resolved.
/// <para>
/// Decisions (<see cref="Report"/>, <see cref="Take"/>, ...) check the rules and return the event to append; the
/// <c>On</c> methods (reached only through <see cref="Evolve"/>) fold events and never refuse one - a stored event is
/// a fact (ADR-0003). They are private on purpose: Marten must use the projection's explicit Evolve, not conventions.
/// </para>
/// </summary>
public sealed class Ticket
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string CategoryKey { get; private set; } = "";
    public string SubcategoryKey { get; private set; } = "";
    public Priority Priority { get; private set; }
    public Priority OriginalPriority { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }
    public Requester Requester { get; private set; } = Requester.Anonymous;
    public TicketStatus Status { get; private set; }
    public AgentRef? Assignee { get; private set; }

    /// <summary>The person (not an agent) who does the work, named by the agent (spec 008).</summary>
    public PersonRef? Handler { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public bool ResolvedLate { get; private set; }
    public int Comments { get; private set; }
    public int Attachments { get; private set; }

    /// <summary>The report data (spec 008), by field key; only the values that were given.</summary>
    public IReadOnlyDictionary<string, string> Fields => fields;

    private readonly Dictionary<string, string> fields = [];

    /// <summary>Always from the report date with the current priority (ADR-0004).</summary>
    public DateTimeOffset Deadline => Sla.DeadlineFor(ReportedAt, Priority);

    public DeadlineState StateAt(DateTimeOffset now) => Deadlines.Evaluate(Status, Deadline, now);

    // ---------------------------------------------------------------- decisions

    public static TicketReported Report(
        Guid ticketId,
        TicketDraft draft,
        DateTimeOffset now,
        CategoryCatalog? catalog = null
    )
    {
        var title = TicketLimits.Required(draft.Title, "Tytuł", TicketLimits.TitleMax);
        var description = TicketLimits.Required(draft.Description, "Opis", TicketLimits.DescriptionMax);
        catalog ??= CategoryCatalog.Default;
        catalog.EnsureSelectable(draft.CategoryKey, draft.SubcategoryKey);
        var fields = ReportFields.Check(
            catalog.FindSubcategory(draft.SubcategoryKey)!.Value.Subcategory.ReportFields,
            draft.Fields
        );

        if (draft.Priority is not { } priority || !Enum.IsDefined(priority))
            throw new DomainException("Wybierz priorytet.");

        if (draft.ReportedAt > now + TicketLimits.FutureTolerance)
            throw new DomainException("Data zgłoszenia nie może być w przyszłości.");
        if (draft.ReportedAt < now - TicketLimits.MaxBackdating)
            throw new DomainException("Data zgłoszenia może być najwyżej 30 dni wstecz.");

        draft.Requester.EnsureValid();

        switch (draft.Channel)
        {
            case ReportChannel.Agent when draft.RegisteredBy is null:
                throw new DomainException("Zgłoszenie rejestrowane przez agenta musi wskazywać agenta.");
            case ReportChannel.SelfService when draft.RegisteredBy is not null:
                throw new DomainException("Zgłoszenie samodzielne nie ma agenta rejestrującego.");
        }

        return new TicketReported(
            ticketId,
            title,
            description,
            draft.CategoryKey!,
            draft.SubcategoryKey!,
            priority,
            draft.ReportedAt.ToUniversalTime(),
            draft.Requester,
            draft.Channel,
            draft.RegisteredBy,
            now.ToUniversalTime(),
            fields.Count > 0 ? fields : null
        );
    }

    /// <summary>
    /// Fill in or correct the report data (spec 008); <paramref name="values"/> is the whole new set - a missing or empty
    /// value clears the field. Allowed in any state, like a comment: a number often arrives after the work is done.
    /// Null when nothing changed.
    /// </summary>
    public TicketFieldsUpdated? UpdateFields(
        AgentRef agent,
        IReadOnlyDictionary<string, string?> values,
        DateTimeOffset now,
        CategoryCatalog? catalog = null
    )
    {
        var definitions =
            (catalog ?? CategoryCatalog.Default).FindSubcategory(SubcategoryKey)?.Subcategory.ReportFields ?? [];
        if (definitions.Count == 0)
            throw new DomainException("Ta podkategoria nie ma danych do raportu.");

        var next = ReportFields.Check(definitions, values);
        var changes = definitions
            .Select(d => new FieldChange(
                d.Key,
                d.Label,
                fields.GetValueOrDefault(d.Key),
                next.GetValueOrDefault(d.Key)
            ))
            .Where(c => c.From != c.To)
            .ToList();
        return changes.Count == 0 ? null : new TicketFieldsUpdated(Id, changes, agent, now.ToUniversalTime());
    }

    /// <summary>
    /// Name the person who does the work, or take them off (<paramref name="person"/> null). Only the agent the ticket is
    /// with, and only somebody from the category's department when it has one. Null when nothing changes.
    /// </summary>
    public TicketHandlerAssigned? AssignHandler(
        AgentRef agent,
        PersonRef? person,
        string? personDepartment,
        string? reason,
        DateTimeOffset now,
        CategoryCatalog? catalog = null
    )
    {
        EnsureAssignedTo(agent, "wskazać osobę realizującą");
        if (
            person is not null
            && (catalog ?? CategoryCatalog.Default).FindCategory(CategoryKey)?.Department is { } department
            && !string.Equals(personDepartment, department, StringComparison.OrdinalIgnoreCase)
        )
            throw new DomainException($"Wybierz osobę z działu {department}.");
        if (person?.Id == Handler?.Id)
            return null;
        var why = TicketLimits.Optional(reason, "Powód", TicketLimits.ReasonMax);
        return new TicketHandlerAssigned(Id, person, agent, why, now.ToUniversalTime());
    }

    public TicketTaken Take(AgentRef agent, DateTimeOffset now)
    {
        switch (Status)
        {
            case TicketStatus.InProgress:
                throw new DomainException($"Zgłoszenie jest już w realizacji u: {Assignee?.FullName}.");
            case TicketStatus.Done:
                throw new DomainException("Zgłoszenie jest już zrealizowane.");
        }
        return new TicketTaken(Id, agent, now.ToUniversalTime());
    }

    public TicketHandedOver HandOver(AgentRef from, AgentRef to, string? reason, DateTimeOffset now)
    {
        EnsureAssignedTo(from, "przekazać");
        if (to.Id == from.Id)
            throw new DomainException("Nie można przekazać zgłoszenia samemu sobie.");
        var why = TicketLimits.Optional(reason, "Powód", TicketLimits.ReasonMax);
        return new TicketHandedOver(Id, from, to, why, now.ToUniversalTime());
    }

    public TicketReturnedToPool ReturnToPool(AgentRef agent, string? reason, DateTimeOffset now)
    {
        EnsureAssignedTo(agent, "oddać do puli");
        var why = TicketLimits.Optional(reason, "Powód", TicketLimits.ReasonMax);
        return new TicketReturnedToPool(Id, agent, why, now.ToUniversalTime());
    }

    /// <summary>Null when the priority is already the requested one: nothing happened, nothing is stored.</summary>
    public TicketPriorityChanged? ChangePriority(AgentRef agent, Priority to, string? reason, DateTimeOffset now)
    {
        if (Status == TicketStatus.Done)
            throw new DomainException("Nie można zmienić priorytetu zrealizowanego zgłoszenia.");
        if (!Enum.IsDefined(to))
            throw new DomainException("Wybierz priorytet.");
        if (to == Priority)
            return null;
        var why = TicketLimits.Optional(reason, "Powód", TicketLimits.ReasonMax);
        return new TicketPriorityChanged(Id, Priority, to, agent, why, now.ToUniversalTime());
    }

    public TicketResolved Resolve(AgentRef agent, DateTimeOffset now)
    {
        if (Status == TicketStatus.Done)
            throw new DomainException("Zgłoszenie jest już zrealizowane.");
        EnsureAssignedTo(agent, "oznaczyć jako zrealizowane");
        return new TicketResolved(Id, agent, now.ToUniversalTime(), Late: now >= Deadline);
    }

    public TicketCommented Comment(Guid commentId, AgentRef agent, string? text, DateTimeOffset now)
    {
        var body = TicketLimits.Required(text, "Komentarz", TicketLimits.CommentMax);
        return new TicketCommented(Id, commentId, agent, body, now.ToUniversalTime());
    }

    /// <summary>
    /// Attach a file. <paramref name="agent"/> is null for the requester on the public form; then
    /// <paramref name="addedBy"/> is the requester's display name. Allowed in any state, like a comment.
    /// </summary>
    public TicketAttachmentAdded AddAttachment(
        Guid attachmentId,
        string? fileName,
        long size,
        AgentRef? agent,
        string addedBy,
        DateTimeOffset now
    )
    {
        if (Attachments >= AttachmentRules.MaxPerTicket)
            throw new DomainException($"Zgłoszenie może mieć najwyżej {AttachmentRules.MaxPerTicket} załączników.");
        var (name, type) = AttachmentRules.Check(fileName, size);
        return new TicketAttachmentAdded(
            Id,
            attachmentId,
            name,
            type,
            size,
            StorageKeyFor(Id, attachmentId),
            agent?.FullName ?? addedBy,
            agent,
            now.ToUniversalTime()
        );
    }

    public static string StorageKeyFor(Guid ticketId, Guid attachmentId) => $"tickets/{ticketId:N}/{attachmentId:N}";

    private void EnsureAssignedTo(AgentRef agent, string action)
    {
        switch (Status)
        {
            case TicketStatus.InPool:
                throw new DomainException($"Zgłoszenie jest w puli - najpierw trzeba je przejąć, żeby {action}.");
            case TicketStatus.Done:
                throw new DomainException("Zgłoszenie jest już zrealizowane.");
        }
        if (Assignee?.Id != agent.Id)
            throw new DomainException($"Tylko agent realizujący zgłoszenie ({Assignee?.FullName}) może je {action}.");
    }

    // ---------------------------------------------------------------- folding

    /// <summary>Folds one event; used by Marten's live aggregation and by the tests.</summary>
    public static Ticket Evolve(Ticket? ticket, object @event)
    {
        ticket ??= new Ticket();
        switch (@event)
        {
            case TicketReported e:
                ticket.On(e);
                break;
            case TicketTaken e:
                ticket.On(e);
                break;
            case TicketHandedOver e:
                ticket.On(e);
                break;
            case TicketReturnedToPool e:
                ticket.On(e);
                break;
            case TicketPriorityChanged e:
                ticket.On(e);
                break;
            case TicketResolved e:
                ticket.On(e);
                break;
            case TicketCommented e:
                ticket.On(e);
                break;
            case TicketAttachmentAdded e:
                ticket.On(e);
                break;
            case TicketFieldsUpdated e:
                ticket.On(e);
                break;
            case TicketHandlerAssigned e:
                ticket.On(e);
                break;
        }
        return ticket;
    }

    public static Ticket FromEvents(IEnumerable<object> events) =>
        events.Aggregate((Ticket?)null, Evolve) ?? new Ticket();

    private void On(TicketReported e)
    {
        Id = e.TicketId;
        Title = e.Title;
        Description = e.Description;
        CategoryKey = e.CategoryKey;
        SubcategoryKey = e.SubcategoryKey;
        Priority = e.Priority;
        OriginalPriority = e.Priority;
        ReportedAt = e.ReportedAt;
        Requester = e.Requester;
        Status = TicketStatus.InPool;
        foreach (var (key, value) in e.Fields ?? new Dictionary<string, string>())
            fields[key] = value;
    }

    private void On(TicketTaken e)
    {
        Status = TicketStatus.InProgress;
        Assignee = e.Agent;
    }

    private void On(TicketHandedOver e)
    {
        Status = TicketStatus.InProgress;
        Assignee = e.To;
    }

    private void On(TicketReturnedToPool e)
    {
        Status = TicketStatus.InPool;
        Assignee = null;
    }

    private void On(TicketPriorityChanged e) => Priority = e.To;

    private void On(TicketResolved e)
    {
        Status = TicketStatus.Done;
        ResolvedAt = e.At;
        ResolvedLate = e.Late;
    }

    private void On(TicketCommented e) => Comments++;

    private void On(TicketAttachmentAdded e) => Attachments++;

    private void On(TicketHandlerAssigned e) => Handler = e.Person;

    private void On(TicketFieldsUpdated e)
    {
        foreach (var change in e.Changes)
            if (change.To is null)
                fields.Remove(change.Key);
            else
                fields[change.Key] = change.To;
    }

    /// <summary>
    /// The short number a requester is given: "HAL-" and 8 hex digits of the id. For a version 7 id (every reported
    /// ticket) the first digits are its timestamp - two tickets within ~65 s shared a number - so its last 8 (random)
    /// digits are used; other ids (the seed's name-based ones) keep their first 8. Stored in the view at the report, so
    /// a number already given never changes.
    /// </summary>
    public static string NumberOf(Guid id)
    {
        var hex = id.ToString("N").ToUpperInvariant();
        return "HAL-" + (id.Version == 7 ? hex[^8..] : hex[..8]);
    }
}
