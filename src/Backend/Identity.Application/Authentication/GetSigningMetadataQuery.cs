using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record GetSigningMetadataQuery : IRequest<SigningMetadata>;
