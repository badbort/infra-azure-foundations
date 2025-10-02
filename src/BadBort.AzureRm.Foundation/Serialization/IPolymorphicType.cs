namespace BadBort.AzureRm.Foundation.Serialization;

public interface IPolymorphicType
{
    string? Type { get; }

    static abstract Dictionary<string, Type> GetTypes();
}