using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.BufferedDeserialization.TypeDiscriminators;
using YamlDotNet.Serialization.NamingConventions;

namespace BadBort.AzureRm.Foundation.Infra.Serialization;

public static class YamlUtility
{
    /// <summary>
    /// Returns preconfigured Serializer/Deserializer builders using snake_case naming.
    /// You can further customize (e.g., type discriminators, converters) via the delegates.
    /// </summary>
    public static YamlConfiguration GetConfiguration(
        Action<SerializerBuilder>? configureSerializer = null,
        Action<DeserializerBuilder>? configureDeserializer = null,
        bool ignoreUnmatchedProperties = true)
    {
        var (ser, de) = CreateDefaultBuilders(ignoreUnmatchedProperties);

        configureSerializer?.Invoke(ser);
        configureDeserializer?.Invoke(de);

        return new YamlConfiguration(ser, de);
    }

    public static string Serialize<T>(
        T value,
        Action<SerializerBuilder>? configure = null)
    {
        var config = GetConfiguration(configure, null);
        var serializer = config.SerializerBuilder
            // Common defaults; adjust as desired
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
            .Build();

        return serializer.Serialize(value);
    }

    public static void Serialize<T>(
        Stream output,
        T value,
        Action<SerializerBuilder>? configure = null,
        Encoding? encoding = null)
    {
        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var writer = new StreamWriter(output, encoding, leaveOpen: true);
        writer.Write(Serialize(value, configure));
        writer.Flush();
    }

    public static void SerializeToFile<T>(
        string path,
        T value,
        Action<SerializerBuilder>? configure = null,
        Encoding? encoding = null)
    {
        encoding ??= new UTF8Encoding(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(value, configure), encoding);
    }

    public static T? Deserialize<T>(
        string yaml,
        Action<DeserializerBuilder>? configure = null,
        bool ignoreUnmatchedProperties = true)
    {
        var config = GetConfiguration(null, configure, ignoreUnmatchedProperties);
        var deserializer = config.DeserializerBuilder.Build();
        return deserializer.Deserialize<T>(yaml);
    }

    public static T Deserialize<T>(
        Stream input,
        Action<DeserializerBuilder>? configure = null,
        bool ignoreUnmatchedProperties = true,
        Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        using var reader = new StreamReader(input, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var yaml = reader.ReadToEnd();
        return Deserialize<T>(yaml, configure, ignoreUnmatchedProperties);
    }

    public static T? DeserializeFromFile<T>(
        string path,
        Action<DeserializerBuilder>? configure = null,
        bool ignoreUnmatchedProperties = true,
        Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var yaml = File.ReadAllText(path, encoding);
        return Deserialize<T>(yaml, configure, ignoreUnmatchedProperties);
    }

    public static async Task SerializeToFileAsync<T>(
        string path,
        T value,
        Action<SerializerBuilder>? configure = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        encoding ??= new UTF8Encoding(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = Serialize(value, configure);
        await File.WriteAllTextAsync(path, text, encoding, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T> DeserializeFromFileAsync<T>(
        string path,
        Action<DeserializerBuilder>? configure = null,
        bool ignoreUnmatchedProperties = true,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;
        var yaml = await File.ReadAllTextAsync(path, encoding, cancellationToken).ConfigureAwait(false);
        return Deserialize<T>(yaml, configure, ignoreUnmatchedProperties);
    }

    private static (SerializerBuilder Serializer, DeserializerBuilder Deserializer) CreateDefaultBuilders(bool ignoreUnmatchedProperties)
    {
        var ser = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance);

        var de = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance);

        if (ignoreUnmatchedProperties)
        {
            de = de.IgnoreUnmatchedProperties();
        }

        de.WithDuplicateKeyChecking();

        var typeDiscriminators = GetTypeDiscriminators(Assembly.GetExecutingAssembly());
        de = de.WithTypeDiscriminatingNodeDeserializer(options =>
        {
            typeDiscriminators.ForEach(options.AddTypeDiscriminator);
        });

        return (ser, de);
    }

    internal static List<KeyValueTypeDiscriminator> GetTypeDiscriminators(Assembly assembly)
    {
        var polymorphicTypes = assembly.GetTypes().Where(t => typeof(IPolymorphicType).IsAssignableFrom(t)).Distinct().ToList();
        var typeDiscriminators = new List<KeyValueTypeDiscriminator>();

        foreach (var type in polymorphicTypes)
        {
            if(type == typeof(IPolymorphicType))
                continue;

            var getTypesMethod = type.GetMethod(nameof(IPolymorphicType.GetTypes), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (getTypesMethod == null)
            {
                continue;
            }
            
            var typeMap = (Dictionary<string, Type>?)getTypesMethod.Invoke(null,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
                null, parameters: null, null);
            if(typeMap == null)
                continue;
            
            var discriminator = new KeyValueTypeDiscriminator(type, "type", typeMap);
            typeDiscriminators.Add(discriminator);
        }
        
        return typeDiscriminators;
    }

    public sealed record YamlConfiguration(
        SerializerBuilder SerializerBuilder,
        DeserializerBuilder DeserializerBuilder);
}