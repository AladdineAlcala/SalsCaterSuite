namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SalsCaterSuite.WebApi.Logging;
using Xunit;

/// <summary>
/// Verifies BR-21 — the owner-ruled redaction rule (DEC-093). A credential or a customer contact
/// identifier must never reach a log file; the rule is applied by property name, at any depth,
/// case-insensitively, to the request body, the response body and the query string alike.
/// </summary>
public class SensitiveDataRedactorTests
{
    private static SensitiveDataRedactor CreateRedactor(Action<RequestLoggingOptions>? configure = null)
    {
        var options = new RequestLoggingOptions();
        configure?.Invoke(options);

        return new SensitiveDataRedactor(Options.Create(options));
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    // ── The secret set ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("password")]
    [InlineData("passwordconfirmation")]
    [InlineData("newpassword")]
    [InlineData("confirmpassword")]
    [InlineData("token")]
    [InlineData("accesstoken")]
    [InlineData("refreshtoken")]
    [InlineData("idtoken")]
    [InlineData("jwt")]
    [InlineData("secret")]
    [InlineData("secretkey")]
    [InlineData("apikey")]
    [InlineData("api_key")]
    [InlineData("authorization")]
    [InlineData("clientsecret")]
    [InlineData("signature")]
    [InlineData("privatekey")]
    [InlineData("connectionstring")]
    public void RedactJsonBody_WhenPropertyIsASecret_ReplacesTheValue(string propertyName)
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody($"{{\"{propertyName}\":\"super-secret-value\"}}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty(propertyName).GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        result.Should().NotContain("super-secret-value");
    }

    // ── The account identifier set (DEC-096) ────────────────────────────────

    [Theory]
    [InlineData("username")]
    [InlineData("userName")]
    [InlineData("createdbyuser")]
    public void RedactJsonBody_WhenPropertyIsAnAccountIdentifier_ReplacesTheValue(string propertyName)
    {
        // In this deployment the username IS the administrator's e-mail address, so an
        // unredacted username is a contact identifier on disk (DEC-096).
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody($"{{\"{propertyName}\":\"admin@salscatersuite.local\"}}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty(propertyName).GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        result.Should().NotContain("admin@salscatersuite.local");
    }

    // ── The customer contact identifier set (DEC-093) ────────────────────────

    [Theory]
    [InlineData("address")]
    [InlineData("contact")]
    [InlineData("contact1")]
    [InlineData("contact2")]
    [InlineData("customercontact")]
    public void RedactJsonBody_WhenPropertyIsACustomerContactIdentifier_ReplacesTheValue(string propertyName)
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody($"{{\"{propertyName}\":\"1 Integration Way\"}}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty(propertyName).GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        result.Should().NotContain("1 Integration Way");
    }

    // ── The forward-looking set (no property in the model carries them) ──────

    [Theory]
    [InlineData("email")]
    [InlineData("emailaddress")]
    [InlineData("tin")]
    public void RedactJsonBody_WhenPropertyIsForwardLooking_ReplacesTheValue(string propertyName)
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody($"{{\"{propertyName}\":\"customer-identifier\"}}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty(propertyName).GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        result.Should().NotContain("customer-identifier");
    }

    [Fact]
    public void RedactJsonBody_WhenBodyCarriesACustomerNameAndAnAmount_KeepsBothVisible()
    {
        // BR-21.8 / DEC-093 — the log has to stay operationally useful: a name and an amount
        // are deliberately NOT redacted. This is a data-exposure decision, not an oversight.
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody("{\"customerName\":\"Maria Santos\",\"amountDue\":12500.75}");

        result.Should().NotBeNull();
        var parsed = Parse(result!);
        parsed.GetProperty("customerName").GetString().Should().Be("Maria Santos");
        parsed.GetProperty("amountDue").GetDecimal().Should().Be(12500.75m);
    }

    // ── Case insensitivity, nesting, arrays ─────────────────────────────────

    [Theory]
    [InlineData("PassWord")]
    [InlineData("PASSWORD")]
    [InlineData("passWord")]
    [InlineData("UserName")]
    [InlineData("USERNAME")]
    [InlineData("CreatedByUser")]
    [InlineData("CustomerContact")]
    public void RedactJsonBody_WhenPropertyNameDiffersInCase_StillRedacts(string propertyName)
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody($"{{\"{propertyName}\":\"hunter2\"}}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty(propertyName).GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
    }

    [Fact]
    public void RedactJsonBody_WhenTheSecretIsNested_RedactsAtAnyDepth()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody(
            "{\"customer\":{\"profile\":{\"credentials\":{\"password\":\"hunter2\"}},\"name\":\"Maria\"}," +
            "\"audit\":{\"createdByUser\":\"admin@salscatersuite.local\"}}");

        result.Should().NotBeNull();
        var parsed = Parse(result!);
        parsed.GetProperty("customer").GetProperty("profile").GetProperty("credentials")
            .GetProperty("password").GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        parsed.GetProperty("customer").GetProperty("name").GetString().Should().Be("Maria");
        parsed.GetProperty("audit").GetProperty("createdByUser").GetString()
            .Should().Be(SensitiveDataRedactor.RedactionMarker);
        result.Should().NotContain("admin@salscatersuite.local");
    }

    [Fact]
    public void RedactJsonBody_WhenTheSecretIsInsideAnArray_RedactsEveryOccurrence()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody(
            "{\"contacts\":[{\"customerContact\":\"09170000000\",\"label\":\"primary\"}," +
            "{\"customerContact\":\"09180000000\"}]}");

        result.Should().NotBeNull();
        var parsed = Parse(result!);
        var contacts = parsed.GetProperty("contacts");

        contacts[0].GetProperty("customerContact").GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        contacts[1].GetProperty("customerContact").GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        contacts[0].GetProperty("label").GetString().Should().Be("primary");
        result.Should().NotContain("09170000000");
    }

    [Fact]
    public void RedactJsonBody_WhenASensitivePropertyIsNull_StillReplacesItWithTheMarker()
    {
        // BR-21.2 — the *name* decides, not the value.
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody("{\"token\":null,\"other\":null}");

        result.Should().NotBeNull();
        var parsed = Parse(result!);
        parsed.GetProperty("token").GetString().Should().Be(SensitiveDataRedactor.RedactionMarker);
        parsed.GetProperty("other").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void RedactJsonBody_WhenANonSensitivePropertyIsNull_LeavesItNull()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody("{\"occasion\":null}");

        result.Should().NotBeNull();
        Parse(result!).GetProperty("occasion").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Bodies that cannot be logged ─────────────────────────────────────────

    [Fact]
    public void RedactJsonBody_WhenTheBodyIsNotJson_ReturnsTheNonJsonMarker()
    {
        // BR-21.6 — a malformed body is exactly the shape in which a credential could hide
        // from a name-based redactor, so it is never written.
        var redactor = CreateRedactor();

        var result = redactor.RedactJsonBody("username=admin&password=hunter2");

        result.Should().Be(SensitiveDataRedactor.NonJsonBodyMarker);
        result.Should().NotContain("hunter2");
    }

    [Fact]
    public void RedactJsonBody_WhenTheBodyIsNull_ReturnsNull()
    {
        var redactor = CreateRedactor();

        redactor.RedactJsonBody(null).Should().BeNull();
    }

    [Fact]
    public void RedactJsonBody_WhenTheBodyIsEmpty_ReturnsNull()
    {
        var redactor = CreateRedactor();

        redactor.RedactJsonBody(string.Empty).Should().BeNull();
        redactor.RedactJsonBody("   ").Should().BeNull();
    }

    // ── The query string (BR-21.4) ───────────────────────────────────────────

    [Fact]
    public void RedactQueryString_WhenAParameterIsSensitive_RedactsOnlyThatParameter()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactQueryString("?customerType=Corporate&email=maria@example.test&page=2");

        result.Should().NotBeNull();
        result.Should().Contain("customerType=Corporate");
        result.Should().Contain("page=2");
        result.Should().Contain($"email={SensitiveDataRedactor.RedactionMarker}");
        result.Should().NotContain("maria@example.test");
    }

    [Fact]
    public void RedactQueryString_WhenTheAccessTokenParameterIsPresent_RedactsIt()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactQueryString("?access_token=eyJhbGciOi.super-secret");

        result.Should().NotBeNull();
        result.Should().Be($"access_token={SensitiveDataRedactor.RedactionMarker}");
        result.Should().NotContain("eyJhbGciOi");
    }

    [Fact]
    public void RedactQueryString_WhenTheSearchParameterIsPresent_RedactsIt()
    {
        // BR-21.4 — "search" matches a customer name, a company and a contact number.
        var redactor = CreateRedactor();

        var result = redactor.RedactQueryString("?search=Maria+Santos&page=2");

        result.Should().NotBeNull();
        result.Should().Be($"search={SensitiveDataRedactor.RedactionMarker}&page=2");
        result.Should().NotContain("Maria");
        result.Should().Contain("page=2");
    }

    [Fact]
    public void RedactQueryString_WhenThereIsNoQueryString_ReturnsNull()
    {
        var redactor = CreateRedactor();

        redactor.RedactQueryString(null).Should().BeNull();
        redactor.RedactQueryString(string.Empty).Should().BeNull();
        redactor.RedactQueryString("?").Should().BeNull();
    }

    [Fact]
    public void RedactQueryString_WhenTheParameterNameIsEncoded_StillRedacts()
    {
        var redactor = CreateRedactor();

        var result = redactor.RedactQueryString("?access%5Ftoken=secret-value");

        result.Should().NotBeNull();
        result.Should().NotContain("secret-value");
    }

    // ── The record's own field names (BR-21.3) ──────────────────────────────

    [Fact]
    public void RedactRecordFields_WhenAFieldNameIsSensitive_ReplacesItsValueWhateverItIs()
    {
        // BR-21.3 — the sensitive-name set applies to the record's OWN field names as well as to
        // the body's property names. The rule is general: the value does not matter, the name does.
        var redactor = CreateRedactor();

        var fields = new Dictionary<string, object?>
        {
            ["UserName"] = "admin@salscatersuite.local",
            ["UserId"] = "4adc482d-d68e-451c-8b9d-53a022bdd30a",
            ["Roles"] = new[] { "Administrator" },
            ["StatusCode"] = 200,
            ["Contact"] = "09170000000"
        };

        redactor.RedactRecordFields(fields);

        fields["UserName"].Should().Be(SensitiveDataRedactor.RedactionMarker);
        fields["Contact"].Should().Be(SensitiveDataRedactor.RedactionMarker);

        // A field whose name is not in the set is untouched.
        fields["UserId"].Should().Be("4adc482d-d68e-451c-8b9d-53a022bdd30a");
        fields["Roles"].Should().BeEquivalentTo(new[] { "Administrator" });
        fields["StatusCode"].Should().Be(200);
    }

    [Fact]
    public void RedactRecordFields_WhenAFieldValueIsNull_LeavesItNull()
    {
        // "null" means absent — a null value carries no secret, so it is not replaced.
        var redactor = CreateRedactor();

        var fields = new Dictionary<string, object?> { ["UserName"] = null };

        redactor.RedactRecordFields(fields);

        fields["UserName"].Should().BeNull();
    }

    [Fact]
    public void RedactRecordFields_MatchesTheNameCaseInsensitively()
    {
        var redactor = CreateRedactor();

        var fields = new Dictionary<string, object?>
        {
            ["USERNAME"] = "admin@salscatersuite.local",
            ["createdbyuser"] = "admin@salscatersuite.local"
        };

        redactor.RedactRecordFields(fields);

        fields["USERNAME"].Should().Be(SensitiveDataRedactor.RedactionMarker);
        fields["createdbyuser"].Should().Be(SensitiveDataRedactor.RedactionMarker);
    }

    // ── The header rule (BR-21.5) ───────────────────────────────────────────

    [Fact]
    public void HasAuthorizationHeader_WhenTheHeaderIsPresent_ReturnsTrue()
    {
        var headers = new HeaderDictionary { ["Authorization"] = "Bearer super-secret-token" };

        SensitiveDataRedactor.HasAuthorizationHeader(headers).Should().BeTrue();
    }

    [Fact]
    public void HasAuthorizationHeader_WhenTheHeaderIsAbsent_ReturnsFalse()
    {
        SensitiveDataRedactor.HasAuthorizationHeader(new HeaderDictionary()).Should().BeFalse();
        SensitiveDataRedactor.HasAuthorizationHeader(null).Should().BeFalse();
    }

    [Fact]
    public void RedactHeaderValue_NeverReturnsTheHeaderValue()
    {
        // BR-21.5 — no header value is ever written to a log, not even the Authorization value.
        var result = SensitiveDataRedactor.RedactHeaderValue("Authorization", "Bearer super-secret-token");

        result.Should().Be(SensitiveDataRedactor.HeaderValuesOmittedMarker);
        result.Should().NotContain("super-secret-token");
    }
}
