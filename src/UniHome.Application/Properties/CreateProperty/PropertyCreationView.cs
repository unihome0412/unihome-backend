using UniHome.Domain.Properties;

namespace UniHome.Application.Properties.CreateProperty;

public sealed record PropertyCreationView(Property Property, bool LocationSuggested);
