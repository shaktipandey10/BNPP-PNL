using System.Text;
using PnlCapture.Infrastructure.Messaging;

namespace PnlCapture.Infrastructure.Tests;

public sealed class PnlMessageTests
{
    [Fact]
    public void Deserialize_AcceptsDocumentedCamelCaseContract()
    {
        const string json =
            """{"messageId":"message-1","sourceSystem":"Reuters","accountNumber":12345,"pnlAmount":-12.75}""";

        var message = PnlMessage.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.Equal("message-1", message.MessageId);
        Assert.Equal("Reuters", message.SourceSystem);
        Assert.Equal(12345, message.AccountNumber);
        Assert.Equal(-12.75m, message.PnlAmount);
    }

    [Theory]
    [InlineData("""{"sourceSystem":"Reuters","accountNumber":12345,"pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","accountNumber":12345,"pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","sourceSystem":"Reuters","pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","sourceSystem":"Reuters","accountNumber":12345}""")]
    public void Deserialize_RejectsMissingRequiredFields(string json)
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => PnlMessage.Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("""{"messageId":" ","sourceSystem":"Reuters","accountNumber":12345,"pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","sourceSystem":"","accountNumber":12345,"pnlAmount":12.75}""")]
    [InlineData("""{"messageId":null,"sourceSystem":"Reuters","accountNumber":12345,"pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","sourceSystem":null,"accountNumber":12345,"pnlAmount":12.75}""")]
    public void Deserialize_RejectsEmptyRequiredStrings(string json)
    {
        Assert.Throws<InvalidDataException>(
            () => PnlMessage.Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("""{"messageId":"message-1","sourceSystem":"Reuters","accountNumber":"12345","pnlAmount":12.75}""")]
    [InlineData("""{"messageId":"message-1","sourceSystem":"Reuters","accountNumber":12345,"pnlAmount":"not-a-decimal"}""")]
    public void Deserialize_RejectsInvalidNumericFields(string json)
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => PnlMessage.Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => PnlMessage.Deserialize(Encoding.UTF8.GetBytes("{malformed")));
    }
}
