namespace Identity.Application.BulkData;

public sealed class BulkDescriptorCatalog : IBulkDescriptorCatalog
{
    private readonly Dictionary<string, BulkEntityDefinition> definitions;

    public BulkDescriptorCatalog(
        IEnumerable<BulkEntityDescriptor> descriptors,
        IEnumerable<IBulkEntityValidator> validators,
        IEnumerable<IBulkEntityCommitter> committers)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(committers);
        var validatorsByKey = validators
            .GroupBy(validator => validator.EntityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var committersByKey = committers
            .GroupBy(committer => committer.EntityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        /* Last registration wins rather than throwing. A host that deliberately replaces a
           descriptor should get its replacement, not a startup failure. */
        definitions = new Dictionary<string, BulkEntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            definitions[descriptor.EntityKey] = new BulkEntityDefinition(
                descriptor,
                validatorsByKey.GetValueOrDefault(descriptor.EntityKey),
                committersByKey.GetValueOrDefault(descriptor.EntityKey));
        }
    }

    public bool TryResolve(string entityKey, out BulkEntityDefinition definition) =>
        definitions.TryGetValue(entityKey ?? string.Empty, out definition!);

    public IReadOnlyList<string> EntityKeys => [.. definitions.Keys.Order(StringComparer.Ordinal)];
}
