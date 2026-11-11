using Halcyon.Domain.ValueObjects;
using static Halcyon.Domain.Tests.TestData;

namespace Halcyon.Domain.Tests;

/// <summary>Spec 005.</summary>
public class CommentTests
{
    private static readonly Guid CommentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Comment_IsTrimmed_AndCounted()
    {
        var ticket = Reported();
        var comment = ticket.Comment(CommentId, Piotr, "  sprawdzam  ", Now);

        comment.Text.ShouldBe("sprawdzam");
        comment.Agent.ShouldBe(Piotr);
        ticket.Then(comment).Comments.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void EmptyComment_IsRefused(string? text) =>
        Should.Throw<DomainException>(() => Reported().Comment(CommentId, Anna, text, Now));

    [Fact]
    public void TooLongComment_IsRefused() =>
        Should.Throw<DomainException>(() =>
            Reported().Comment(CommentId, Anna, new string('x', CommentText.MaxLength + 1), Now)
        );

    [Fact]
    public void Comment_OnAResolvedTicket_IsAllowed()
    {
        var ticket = TakenBy(Anna);
        ticket = ticket.Then(ticket.Resolve(Anna, Now));
        Should.NotThrow(() => ticket.Comment(CommentId, Piotr, "dziękujemy", Now));
    }

    [Fact]
    public void Comment_KeepsMarkupAsText() =>
        Reported()
            .Comment(CommentId, Anna, "<script>alert(1)</script>", Now)
            .Text.ShouldBe("<script>alert(1)</script>");
}
