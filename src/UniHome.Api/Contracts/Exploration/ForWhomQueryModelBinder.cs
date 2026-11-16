using Microsoft.AspNetCore.Mvc.ModelBinding;
using UniHome.Domain.Units;

namespace UniHome.Api.Contracts.Exploration;

/// <summary>
/// El contrato HTTP usa nombres string para <see cref="ForWhom"/>. El binder
/// nativo de MVC acepta además valores numéricos; este binder los rechaza para
/// mantener el mismo contrato que el JSON y evitar que Android dependa de
/// ordinales internos.
/// </summary>
public sealed class ForWhomQueryModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (value == ValueProviderResult.None ||
            string.IsNullOrWhiteSpace(value.FirstValue))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var raw = value.FirstValue!;

        if (int.TryParse(raw, out _) ||
            !Enum.TryParse<ForWhom>(raw, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            bindingContext.ModelState.TryAddModelError(
                "paraQuien",
                "El valor de paraQuien no es válido.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success((ForWhom?)parsed);
        return Task.CompletedTask;
    }
}
