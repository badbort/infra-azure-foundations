namespace BadBort.AzureRm.Foundation.Infra.Serialization;

public interface IPolymorphicType
{
    string? Type { get; }

    static abstract Dictionary<string, Type> GetTypes();
}