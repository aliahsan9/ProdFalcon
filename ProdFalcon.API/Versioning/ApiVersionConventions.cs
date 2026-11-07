using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ProdFalcon.API.Versioning;

public sealed class DefaultApiVersionConvention : IControllerConvention
{
    public bool Apply(IControllerConventionBuilder controller, ControllerModel controllerModel)
    {
        controller.HasApiVersion(new ApiVersion(1, 0));
        return true;
    }
}

/// <summary>
/// Keeps existing /api/... routes and adds /api/v{version}/... for the same actions.
/// </summary>
public sealed class VersionedRouteConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var templates = controller.Selectors
                .Select(selector => selector.AttributeRouteModel?.Template)
                .Where(template => !string.IsNullOrWhiteSpace(template))
                .Select(template => template!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var template in templates)
            {
                if (template.Contains("{version", StringComparison.OrdinalIgnoreCase))
                    continue;

                var versioned = ToVersionedTemplate(template);
                if (versioned == null || templates.Contains(versioned, StringComparer.OrdinalIgnoreCase))
                    continue;

                controller.Selectors.Add(new SelectorModel
                {
                    AttributeRouteModel = new AttributeRouteModel(new Microsoft.AspNetCore.Mvc.RouteAttribute(versioned))
                });
            }
        }
    }

    private static string? ToVersionedTemplate(string template)
    {
        if (template.Equals("api", StringComparison.OrdinalIgnoreCase))
            return "api/v{version:apiVersion}";

        const string prefix = "api/";
        if (!template.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return "api/v{version:apiVersion}/" + template[prefix.Length..];
    }
}
