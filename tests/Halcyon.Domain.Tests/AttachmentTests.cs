using Halcyon.Domain.Tickets;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Attachments: what may be attached, and the limit per ticket.</summary>
public class AttachmentTests
{
    private static readonly Guid FileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData("blad.png", "image/png")]
    [InlineData("Zrzut EKRANU.JPG", "image/jpeg")]
    [InlineData("brief.pdf", "application/pdf")]
    [InlineData("kampania.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("spot.mp4", "video/mp4")]
    public void AllowedFile_IsAttached_WithTheTypeFromItsExtension(string name, string type)
    {
        var ticket = Reported();

        var added = ticket.AddAttachment(FileId, name, 1234, Anna, "ignored", Now);

        added.FileName.ShouldBe(name);
        added.ContentType.ShouldBe(type);
        added.Size.ShouldBe(1234);
        added.AddedBy.ShouldBe("Anna Nowak");
        added.StorageKey.ShouldBe($"tickets/{ticket.Id:N}/{FileId:N}");
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("logo.svg")]
    [InlineData("strona.html")]
    [InlineData("bez-rozszerzenia")]
    public void FileOfAnotherType_IsRefused(string name) =>
        Should
            .Throw<DomainException>(() => Reported().AddAttachment(FileId, name, 10, Anna, "", Now))
            .Message.ShouldContain("niedozwolony typ");

    [Fact]
    public void TooBigOrEmptyFile_IsRefused()
    {
        Should
            .Throw<DomainException>(() =>
                Reported().AddAttachment(FileId, "a.png", AttachmentRules.MaxBytes + 1, Anna, "", Now)
            )
            .Message.ShouldContain("za duży");
        Should
            .Throw<DomainException>(() => Reported().AddAttachment(FileId, "a.png", 0, Anna, "", Now))
            .Message.ShouldContain("pusty");
    }

    [Fact]
    public void FoldersInTheName_AreDropped() =>
        Reported().AddAttachment(FileId, "../../etc/zrzut.png", 10, Anna, "", Now).FileName.ShouldBe("zrzut.png");

    [Fact]
    public void FromThePublicForm_TheRequesterIsTheAuthor() =>
        Reported().AddAttachment(FileId, "a.png", 10, null, "Adam Mazur", Now).AddedBy.ShouldBe("Adam Mazur");

    [Fact]
    public void MoreThanTheLimit_IsRefused()
    {
        var ticket = Reported();
        for (var i = 0; i < AttachmentRules.MaxPerTicket; i++)
            ticket = ticket.Then(ticket.AddAttachment(Guid.NewGuid(), $"{i}.png", 10, Anna, "", Now));

        ticket.Attachments.ShouldBe(AttachmentRules.MaxPerTicket);
        Should
            .Throw<DomainException>(() => ticket.AddAttachment(Guid.NewGuid(), "x.png", 10, Anna, "", Now))
            .Message.ShouldContain("najwyżej");
    }

    [Fact]
    public void DoneTicket_StillTakesAttachments()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));

        ticket.AddAttachment(FileId, "protokol.pdf", 10, Anna, "", Now).ShouldNotBeNull();
    }
}
