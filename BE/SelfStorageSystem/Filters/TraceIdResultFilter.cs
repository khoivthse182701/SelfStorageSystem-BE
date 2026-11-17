using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SelfStorageSystem.Contracts.Common;
using System.Diagnostics;

namespace SelfStorageSystem.Filters;

public sealed class TraceIdResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IApiResponse response } && string.IsNullOrWhiteSpace(response.TraceId))
        {
            response.TraceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
