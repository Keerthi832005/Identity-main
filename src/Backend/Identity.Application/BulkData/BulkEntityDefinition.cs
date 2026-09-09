namespace Identity.Application.BulkData;

public sealed record BulkEntityDefinition(
    BulkEntityDescriptor Descriptor,
    IBulkEntityValidator? Validator,
    IBulkEntityCommitter? Committer);
