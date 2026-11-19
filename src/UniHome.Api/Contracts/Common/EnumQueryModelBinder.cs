using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UniHome.Api.Contracts.Common;

/// <summary>
/// Los filtros de consulta documentan enums como string; este binder rechaza
/// valores numéricos para mantener el mismo contrato que el JSON (mismo criterio
/// que <c>ForWhomQueryModelBinder</c>), de forma genérica para cualquier enum.
/// </summary>
public sealed class EnumQueryModelBinder<TEnum> : IModelBinder
    where TEnum : struct, Enum
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (value == ValueProviderResult.None || string.IsNullOrWhiteSpace(value.FirstValue))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var raw = value.FirstValue!;

        if (int.TryParse(raw, out _) ||
            !Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"El valor de {bindingContext.ModelName} no es válido.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success((TEnum?)parsed);
        return Task.CompletedTask;
    }
}
