using Halcyon.Domain.People;

namespace Halcyon.Domain.Tickets;

/// <summary>
/// Who the ticket is from: a person from the list, or nobody in particular (incognito). Never both, never neither.
/// </summary>
public sealed record Requester(bool Incognito, PersonRef? Person)
{
    public static Requester Anonymous { get; } = new(true, null);

    public static Requester For(PersonRef person) => new(false, person);

    public string DisplayName => Incognito || Person is null ? "Incognito" : Person.FullName;

    public void EnsureValid()
    {
        if (Incognito && Person is not null)
            throw new DomainException("Zgłoszenie incognito nie może wskazywać osoby.");
        if (!Incognito && Person is null)
            throw new DomainException("Wybierz zgłaszającego z listy albo zaznacz „incognito”.");
        if (
            Person is not null
            && (string.IsNullOrWhiteSpace(Person.FirstName) || string.IsNullOrWhiteSpace(Person.LastName))
        )
            throw new DomainException("Zgłaszający musi mieć imię i nazwisko.");
    }
}
