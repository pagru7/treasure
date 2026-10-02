using Treasury.App.Domain;

namespace Treasury.Domain.Tests;

public class TransactionTypeTests
{
    [Fact]
    public void Transaction_Type_Uses_Enum()
    {
        var property = typeof(Transaction).GetProperty(nameof(Transaction.Type));

        Assert.NotNull(property);
        Assert.Equal("TransactionType", property!.PropertyType.Name);
        Assert.True(property.PropertyType.IsEnum);
    }
}
