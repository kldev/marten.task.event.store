using Halcyon.Domain.ValueObjects;

namespace Halcyon.Domain.Tests;

/// <summary>
/// ADR-0015: the value objects - each one trims and checks in <c>TryCreate</c> (value or error, never both),
/// <c>Create</c> throws the same message as a <see cref="DomainException"/>, and <c>FromStored</c> reads an event back
/// without the rules.
/// </summary>
public class ValueObjectTests
{
    // ------------------------------------------------------------------------------------------------- free text

    [Fact]
    public void TicketTitle_IsTrimmed()
    {
        var (title, error) = TicketTitle.TryCreate("  Drukarka  ");
        error.ShouldBeNull();
        title!.Value.ShouldBe("Drukarka");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TicketTitle_IsRequired(string? value)
    {
        TicketTitle.TryCreate(value).ShouldBe((null, TicketTitle.RequiredMessage));
        Should.Throw<DomainException>(() => TicketTitle.Create(value)).Message.ShouldBe(TicketTitle.RequiredMessage);
    }

    [Fact]
    public void TicketTitle_HasAMaximum()
    {
        TicketTitle.TryCreate(new string('x', TicketTitle.MaxLength)).error.ShouldBeNull();
        TicketTitle.TryCreate(new string('x', TicketTitle.MaxLength + 1)).error.ShouldBe(TicketTitle.TooLongMessage);
        TicketTitle.TooLongMessage.ShouldContain(TicketTitle.MaxLength.ToString());
    }

    [Fact]
    public void TicketDescription_IsRequired_AndHasAMaximum()
    {
        TicketDescription.TryCreate(" ").error.ShouldBe(TicketDescription.RequiredMessage);
        TicketDescription
            .TryCreate(new string('x', TicketDescription.MaxLength + 1))
            .error.ShouldBe(TicketDescription.TooLongMessage);
        TicketDescription.TooLongMessage.ShouldContain(TicketDescription.MaxLength.ToString());
    }

    [Fact]
    public void CommentText_IsRequired_AndHasAMaximum()
    {
        CommentText.TryCreate(null).error.ShouldBe(CommentText.RequiredMessage);
        CommentText.TryCreate(new string('x', CommentText.MaxLength + 1)).error.ShouldBe(CommentText.TooLongMessage);
        CommentText.TooLongMessage.ShouldContain(CommentText.MaxLength.ToString());
    }

    [Fact]
    public void FromStored_ReadsAnEventBack_WithoutTheRules() =>
        TicketTitle.FromStored(new string('x', TicketTitle.MaxLength + 50)).Value.Length.ShouldBe(250);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Reason_Blank_IsNoReason_AndNoError(string? value)
    {
        Reason.TryCreate(value).ShouldBe((null, null));
        Reason.Create(value).ShouldBeNull();
    }

    [Fact]
    public void Reason_IsTrimmed_AndHasAMaximum()
    {
        Reason.Create("  urlop  ")!.Value.ShouldBe("urlop");
        Should
            .Throw<DomainException>(() => Reason.Create(new string('x', Reason.MaxLength + 1)))
            .Message.ShouldBe(Reason.TooLongMessage);
        Reason.TooLongMessage.ShouldContain(Reason.MaxLength.ToString());
    }

    [Fact]
    public void ValueObjects_AreEqualByValue() => TicketTitle.Create(" A ").ShouldBe(TicketTitle.Create("A"));

    // ------------------------------------------------------------------------------------------------- e-mail

    [Fact]
    public void Email_KeepsWhatWasTyped_AndHasANormalizedForm()
    {
        var email = Email.Create("  Anna.Nowak@Company.io ");
        email.Value.ShouldBe("Anna.Nowak@Company.io");
        email.Normalized.ShouldBe("anna.nowak@company.io");
        Email.Normalize(" X@Y.PL ").ShouldBe("x@y.pl");
    }

    [Theory]
    [InlineData("anna")]
    [InlineData("anna@")]
    [InlineData("Anna <anna@company.io>")]
    [InlineData("a b@company.io")]
    public void Email_NotABareAddress_IsRefused_NamingTheField(string value) =>
        Email
            .TryCreate(value, "E-mail przełożonego")
            .error.ShouldBe("Pole „E-mail przełożonego” musi być adresem e-mail.");

    [Fact]
    public void Email_IsRequired() => Email.TryCreate(" ").error.ShouldBe(Email.RequiredMessage());

    // ------------------------------------------------------------------------------------------------- phone

    [Theory]
    [InlineData("+48 600 100 101", "+48 600 100 101", "+48600100101")]
    [InlineData("  (22)  555-01-02 ", "(22) 555-01-02", "225550102")]
    [InlineData("600100101", "600100101", "600100101")]
    public void PhoneNumber_IsKeptAsWritten_WithSingleSpaces(string value, string stored, string dial)
    {
        var phone = PhoneNumber.Create(value);
        phone.Value.ShouldBe(stored);
        phone.Dial.ShouldBe(dial);
    }

    [Theory]
    [InlineData("123456")] // too few digits
    [InlineData("+48 600 100 101 202 303")] // too many
    [InlineData("600-100-10x")]
    [InlineData("48+600100101")] // "+" only in front
    public void PhoneNumber_ThatIsNotOne_IsRefused(string value) =>
        PhoneNumber.TryCreate(value).ShouldBe((null, PhoneNumber.InvalidMessage));

    [Fact]
    public void PhoneNumber_IsRequired() =>
        Should.Throw<DomainException>(() => PhoneNumber.Create(" ")).Message.ShouldBe(PhoneNumber.RequiredMessage);

    // ------------------------------------------------------------------------------------------------- PESEL

    [Theory]
    [InlineData("92031501232", true)]
    [InlineData(" 92031501232 ", true)]
    [InlineData("92031501233", false)]
    [InlineData("9203150123", false)]
    [InlineData("9203150123a", false)]
    public void Pesel_HasItsCheckDigit(string value, bool valid) =>
        Pesel.TryCreate(value).error.ShouldBe(valid ? null : Pesel.InvalidMessage);

    [Fact]
    public void Pesel_IsRequired() => Pesel.TryCreate(null).error.ShouldBe(Pesel.RequiredMessage);

    // ------------------------------------------------------------------------------------------------- IBAN

    [Theory]
    [InlineData("PL61 1090 1014 0000 0712 1981 2874", "PL61109010140000071219812874")]
    [InlineData("61109010140000071219812874", "PL61109010140000071219812874")]
    [InlineData("de89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    public void Iban_IsCheckedAndWrittenCompact(string value, string stored) =>
        Iban.Create(value).Value.ShouldBe(stored);

    [Theory]
    [InlineData("PL62 1090 1014 0000 0712 1981 2874")] // check digits
    [InlineData("PL61")]
    [InlineData("")]
    public void Iban_ThatIsNotOne_IsRefused_NamingTheField(string value) =>
        Iban.TryCreate(value, "Konto").error.ShouldBe("Pole „Konto” nie jest poprawnym numerem rachunku (IBAN).");

    // ------------------------------------------------------------------------------------------------- amount

    [Theory]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1234.5", "1234.50")]
    [InlineData("-10", "-10.00")]
    [InlineData("1\u00A0000", "1000.00")]
    public void Amount_IsReadAsPeopleWriteIt_AndStoredInvariant(string value, string stored) =>
        Amount.Create(value).Stored.ShouldBe(stored);

    [Theory]
    [InlineData("12,345")]
    [InlineData("abc")]
    [InlineData("1000000000000")]
    [InlineData("")]
    public void Amount_ThatIsNotMoney_IsRefused(string value) =>
        Amount.TryCreate(value, "Kwota netto").error.ShouldBe(Amount.InvalidMessage("Kwota netto"));

    // ------------------------------------------------------------------------------------------------- ticket number

    [Fact]
    public void TicketNumber_ForAnId_IsShortAndStable() =>
        TicketNumber.For(Guid.Parse("0a1b2c3d-0000-0000-0000-000000000000")).Value.ShouldBe("HAL-0A1B2C3D");

    [Fact]
    public void TicketNumber_Typed_IsTrimmedAndInCapitals() =>
        TicketNumber.Create(" hal-1a2b3c4d ").Value.ShouldBe("HAL-1A2B3C4D");

    [Theory]
    [InlineData("HAL-1A2B3C4")]
    [InlineData("HAL-1A2B3C4G")]
    [InlineData("ABC-1A2B3C4D")]
    public void TicketNumber_ThatIsNotOne_IsRefused(string value) =>
        TicketNumber.TryCreate(value).error.ShouldBe(TicketNumber.InvalidMessage());

    [Fact]
    public void TicketNumber_ForAnId_IsAValidTypedOne()
    {
        var number = TicketNumber.For(Guid.CreateVersion7());
        TicketNumber.Create(number.Value).ShouldBe(number);
    }

    // ------------------------------------------------------------------------------------------------- file name

    [Fact]
    public void AttachmentFileName_DropsFolders_AndTakesTheTypeFromTheExtension()
    {
        var name = AttachmentFileName.Create("C:\\Users\\anna\\zrzut ekranu.PNG");
        name.Value.ShouldBe("zrzut ekranu.PNG");
        name.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public void AttachmentFileName_TooLong_IsShortened_KeepingTheExtension()
    {
        var name = AttachmentFileName.Create(new string('a', 300) + ".pdf");
        name.Value.Length.ShouldBe(AttachmentFileName.MaxLength);
        name.Value.ShouldEndWith(".pdf");
    }

    [Theory]
    [InlineData("logo.svg")]
    [InlineData("strona.html")]
    [InlineData("bez-rozszerzenia")]
    public void AttachmentFileName_OfATypeNotAllowed_IsRefused(string value) =>
        AttachmentFileName.TryCreate(value).error.ShouldBe(AttachmentFileName.TypeNotAllowedMessage(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("folder/")]
    [InlineData("\u0001\u0002")]
    public void AttachmentFileName_WithoutAName_IsRefused(string? value) =>
        AttachmentFileName.TryCreate(value).error.ShouldBe(AttachmentFileName.RequiredMessage);
}
