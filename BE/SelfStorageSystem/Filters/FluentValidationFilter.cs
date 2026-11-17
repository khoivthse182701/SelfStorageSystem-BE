using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SelfStorageSystem.Contracts.Common;
using System.Diagnostics;

namespace SelfStorageSystem.Filters;

public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var allErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, argument) in context.ActionArguments)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            foreach (var failure in result.Errors)
            {
                var field = ToCamelCasePath(failure.PropertyName);
                if (!allErrors.TryGetValue(field, out var messages))
                {
                    messages = [];
                    allErrors[field] = messages;
                }
                if (!messages.Contains(failure.ErrorMessage))
                    messages.Add(failure.ErrorMessage);
            }
        }

        if (allErrors.Count > 0)
        {
            var errorDict = allErrors.ToDictionary(k => k.Key, v => v.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            var payload = ApiResponse.Fail("One or more validation errors occurred.", "Validation.Failed", errorDict, traceId);
            context.Result = new ObjectResult(payload) { StatusCode = StatusCodes.Status422UnprocessableEntity };
            return;
        }

        await next();
    }

    private static string ToCamelCasePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "_";
        return string.Join('.', path.Split('.').Select(seg =>
            string.IsNullOrEmpty(seg) ? seg : char.ToLowerInvariant(seg[0]) + seg[1..]));
    }
}
