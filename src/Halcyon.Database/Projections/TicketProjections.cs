using Halcyon.Database.Documents;
using Halcyon.Domain.Tickets;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace Halcyon.Database.Projections;

/// <summary>
/// The write model: Marten folds a ticket's stream into the domain's <see cref="Ticket"/> through its own
/// <see cref="Ticket.Evolve"/>. Explicit code, so the domain project needs no Marten source generator (ADR-0002).
/// Registered as a live aggregation - never stored.
/// </summary>
public sealed class TicketAggregation : SingleStreamProjection<Ticket, Guid>
{
    public TicketAggregation()
    {
        Options.CacheLimitPerTenant = 1000;
    }

    public override Ticket Evolve(Ticket? snapshot, Guid id, IEvent e) => Ticket.Evolve(snapshot, e.Data);
}

/// <summary>The read model of the lists, inline (consistent right after the click).</summary>
public sealed class TicketViewProjection : SingleStreamProjection<TicketView, Guid>
{
    public TicketViewProjection()
    {
        Options.CacheLimitPerTenant = 1000;
    }

    public override TicketView Evolve(TicketView? snapshot, Guid id, IEvent e)
    {
        var view = snapshot ?? new TicketView { Id = id };
        switch (e.Data)
        {
            case TicketReported r:
                view.Id = r.TicketId;
                view.Number = Ticket.NumberOf(r.TicketId);
                view.Title = r.Title;
                view.Description = r.Description;
                view.CategoryKey = r.CategoryKey;
                view.SubcategoryKey = r.SubcategoryKey;
                view.Priority = r.Priority;
                view.OriginalPriority = r.Priority;
                view.ReportedAt = r.ReportedAt;
                view.Deadline = Sla.DeadlineFor(r.ReportedAt, r.Priority);
                view.Status = TicketStatus.InPool;
                view.RequesterIncognito = r.Requester.Incognito;
                view.RequesterId = r.Requester.Person?.Id;
                view.RequesterName = r.Requester.DisplayName;
                view.Channel = r.Channel;
                view.RegisteredByName = r.RegisteredBy?.FullName;
                view.RecordedAt = r.RecordedAt;
                view.LastChangeAt = r.RecordedAt;
                view.Fields = r.Fields is null ? [] : new Dictionary<string, string>(r.Fields);
                break;
            case TicketHandlerAssigned h:
                view.HandlerId = h.Person?.Id;
                view.HandlerName = h.Person?.FullName;
                view.LastChangeAt = h.At;
                break;
            case TicketFieldsUpdated f:
                foreach (var change in f.Changes)
                    if (change.To is null)
                        view.Fields.Remove(change.Key);
                    else
                        view.Fields[change.Key] = change.To;
                view.LastChangeAt = f.At;
                break;
            case TicketTaken t:
                view.Status = TicketStatus.InProgress;
                view.AssigneeId = t.Agent.Id;
                view.AssigneeName = t.Agent.FullName;
                view.LastChangeAt = t.At;
                break;
            case TicketHandedOver h:
                view.Status = TicketStatus.InProgress;
                view.AssigneeId = h.To.Id;
                view.AssigneeName = h.To.FullName;
                view.LastChangeAt = h.At;
                break;
            case TicketReturnedToPool p:
                view.Status = TicketStatus.InPool;
                view.AssigneeId = null;
                view.AssigneeName = null;
                view.LastChangeAt = p.At;
                break;
            case TicketPriorityChanged c:
                view.Priority = c.To;
                view.Deadline = Sla.DeadlineFor(view.ReportedAt, c.To);
                view.LastChangeAt = c.At;
                break;
            case TicketResolved d:
                view.Status = TicketStatus.Done;
                view.ResolvedAt = d.At;
                view.ResolvedLate = d.Late;
                view.LastChangeAt = d.At;
                break;
            case TicketCommented m:
                view.CommentCount++;
                view.LastChangeAt = m.At;
                break;
            case TicketAttachmentAdded a:
                view.Attachments.Add(
                    new AttachmentView
                    {
                        Id = a.AttachmentId,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        Size = a.Size,
                        StorageKey = a.StorageKey,
                        AddedBy = a.AddedBy,
                        At = a.At,
                    }
                );
                view.LastChangeAt = a.At;
                break;
        }
        return view;
    }
}
