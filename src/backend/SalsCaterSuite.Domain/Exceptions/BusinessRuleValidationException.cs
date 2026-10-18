namespace SalsCaterSuite.Domain.Exceptions;

public class BusinessRuleValidationException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleValidationException(string ruleName, string message) : base(message)
    {
        RuleName = ruleName;
    }
}

